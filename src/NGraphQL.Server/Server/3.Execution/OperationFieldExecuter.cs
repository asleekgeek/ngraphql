﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using NGraphQL.CodeFirst;
using NGraphQL.Introspection;
using NGraphQL.Model;
using NGraphQL.Model.Request;
using NGraphQL.Utilities;

namespace NGraphQL.Server.Execution {

  /// <summary>Executes a field of a top-level operation. </summary>
  /// <remarks> We put these methods into a separate class (and not in a RequestHandler) to be able to create 
  /// multiple instances to execute top query fields in parallel. So there will be one RequestHandler and multiple 
  /// OperationFieldExecuter instances. </remarks>
  public partial class OperationFieldExecuter : IOperationFieldContext {
    RequestContext _requestContext;
    OutputObjectScope _parentScope; 
    MappedSelectionField _mappedOpField; 
    List<object> _resolverInstances = new List<object>();
    // this is a flag indicating failure of this operation field; we have more global flag in RequestContext,
    //  but it is for ALL operation fields executing concurrently. We track individual oper field in this _failed
    //  flag, so that we know when to abort this field based on its own errors
    private bool _failed;

    // executed field contexts for fields returning objects; these are pending for process result
    // subsets - we do not do it immediately after executing resolver and getting result objects. 
    // Instead, we save them in this list, and process in a later loop, once all 'current' 
    // resolvers are executed. This is all to make it possible to do batched calls (aka Data Loader)
    List<FieldContext> _executedObjectFieldContexts = new List<FieldContext>();

    public OperationFieldExecuter(RequestContext requestContext, MappedSelectionField opField, OutputObjectScope parentScope) {
      _requestContext = requestContext;
      _parentScope = parentScope;
      _mappedOpField = opField;
    }

    public async Task ExecuteOperationFieldAsync() {
      try {
        var opFieldContext = new FieldContext(_requestContext, this, _mappedOpField);
        opFieldContext.SetCurrentParentScope(_parentScope);
        var result = await InvokeResolverAsync(opFieldContext);
        var opOutValue = opFieldContext.ConvertToOuputValue(result);
        _parentScope.SetValue(_mappedOpField.Field.Key, opOutValue);
        // for fields returning objects, save for further processing of results
        if (opFieldContext.MappedField.Field.SelectionSubset != null)
          _executedObjectFieldContexts.Add(opFieldContext);

        // process object field results until no more
        while (_executedObjectFieldContexts.Count > 0) {
          if (_requestContext.CancellationToken.IsCancellationRequested)
            opFieldContext.ThrowRequestCancelled();
          // save current list, create new one in the field
          var oldFieldContexts = _executedObjectFieldContexts;
          _executedObjectFieldContexts = new List<FieldContext>();
          foreach (var fldCtx in oldFieldContexts) {
            await ExecuteFieldSelectionSubsetAsync(fldCtx);
          }
        }//while
      } finally {
        // notify resolvers about end request
        if (_resolverInstances.Count > 0)
          foreach (var resObj in _resolverInstances)
            (resObj as IResolverClass)?.EndRequest(_requestContext);
      }
    }

    private async Task ExecuteFieldSelectionSubsetAsync(FieldContext parentFieldContext) {
      // all scopes have scope.Entity != null
      var scopes = parentFieldContext.AllResultScopes;
      var outTypeDef = parentFieldContext.TypeDef;
      var selSubSet = parentFieldContext.MappedField.Field.SelectionSubset;
      switch(outTypeDef.Kind) {
        case TypeKind.Object:
          var mappedSubSet = selSubSet.MappedSubSets.FirstOrDefault(ms => ms.Mapping.TypeDef == outTypeDef);
          Util.Check(mappedSubSet != null, 
            $"FATAL: Failed to find mapped selection subset for field '{parentFieldContext.FieldDef.Name}' on type '{outTypeDef.Name}'");
          await ExecuteObjectsSelectionSubsetAsync(parentFieldContext.MappedField, scopes, mappedSubSet);
          return;

        case TypeKind.Interface:
        case TypeKind.Union:
          // group by type, and process each sublist
          var scopesByType = scopes.GroupBy(s => s.Entity.GetType()).ToList();
          foreach(var grp in scopesByType) {
            var entType = grp.Key.GetType();
            var mappedSubSet2 = selSubSet.MappedSubSets.FirstOrDefault(ms => ms.Mapping.EntityType == entType);
            Util.Check(mappedSubSet2 != null, 
              $"FATAL: Failed to find mapped selection subset for field '{parentFieldContext.FieldDef.Name}' for entity type '{entType.Name}'");
            await ExecuteObjectsSelectionSubsetAsync(parentFieldContext.MappedField, grp.ToList(), mappedSubSet2);
          }
          return;

        default:
          return; //never happens
      }
    }

    private async Task ExecuteObjectsSelectionSubsetAsync(MappedSelectionField parentField, 
                 IList<OutputObjectScope> parentScopes, MappedSelectionSubSet mappedSubSet) {

      foreach(var mappedItem in mappedSubSet.MappedItems) {
        
        // TODO: Invoke event to signal execution of directives
        
        // if it is a fragment spread, make recursive call to process fragment fields
        if (mappedItem.Kind == SelectionItemKind.FragmentSpread) {
          var mappedSpread = (MappedFragmentSpread) mappedItem;
          var objTypeDef = mappedSubSet.Mapping.TypeDef;
          var fragmSelSubset = mappedSpread.Spread.Fragment.SelectionSubset;
          var mappedFragmSubset = fragmSelSubset.MappedSubSets.FirstOrDefault(ms => ms.Mapping.TypeDef == objTypeDef);
          await ExecuteObjectsSelectionSubsetAsync(parentField, parentScopes, mappedFragmSubset); //call self recursively
          continue; 
        }

        // It is a plain field
        var mappedField = (MappedSelectionField) mappedItem;
        var fieldContext = new FieldContext(_requestContext, this, mappedField, parentScopes);
        var selFieldKey = mappedField.Field.Key;

        // Process each scope for the field
        foreach (var scope in parentScopes) {
          if (fieldContext.BatchResultWasSet && scope.ContainsKey(selFieldKey))
            continue; 
          fieldContext.SetCurrentParentScope(scope);
          var fldDef = fieldContext.FieldDef;
          object result = await InvokeResolverAsync(fieldContext);
          // if batched result was not set, set value
          if (!fieldContext.BatchResultWasSet) {
            var outValue = fieldContext.ConvertToOuputValue(result);
            scope.SetValue(selFieldKey, outValue);
          }
        } //foreach scope
        // if there are any non-null object-type results, add this field context to this special list
        //   to execute selection subsets in the next round. 
        if (mappedField.Field.SelectionSubset != null && fieldContext.AllResultScopes.Count > 0) {
          _executedObjectFieldContexts.Add(fieldContext);
        }
      } //foreach mappedItem
    } //method

    private object ReadGraphQLObjectValue(FieldDef fldDef, object obj) {
      return ReflectionHelper.GetMemberValue(fldDef.ClrMember, obj);
    }

    private void Fail() {
      _failed = true;
      throw new AbortRequestException();
    }

    public void AbortIfFailed() {
      if (_failed)
        AbortRequest(); 
    }

    public void AbortRequest() {
      throw new AbortRequestException();
    }

    // IOperationFieldContext members
    public string OperationFieldName => _mappedOpField.Field.Name;

    public bool Failed => _failed;

    public void AddError(GraphQLError error) {
      _requestContext.AddError(error);
      _failed = true;
    }


  }
}