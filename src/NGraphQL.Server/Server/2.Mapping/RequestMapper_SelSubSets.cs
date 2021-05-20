﻿using System.Collections.Generic;
using System.Linq;
using NGraphQL.CodeFirst;
using NGraphQL.Core;
using NGraphQL.Model;
using NGraphQL.Server.Execution;
using NGraphQL.Model.Request;
using NGraphQL.Utilities;

namespace NGraphQL.Server.Parsing {

  /// <summary>RequestMapper takes request tree and maps its objects to API model; for ex: selection field is mapped to field definition</summary>
  public partial class RequestMapper {
    /*
    #region pending selection sets list
    // for certain reasons (coming from Fragments and their inter-dependencies) we traverse 
    // the selection tree in wide-first manner; so when we encounter a field with a selection subset,
    // we do not go in immediately, but add the info to this pending list; and the will process it 
    // in the next round
    IList<PendingSelectionSet> _pendingSelectionSets = new List<PendingSelectionSet>();

    class PendingSelectionSet {
      public SelectionSubset SubSet;
      public TypeDefBase OverType;
      public IList<RequestDirective> Directives;
    }
    #endregion

    private void MapOperation(GraphQLOperation op) {
      MapSelectionSubSet(op.SelectionSubset, op.OperationTypeDef, op.Directives);
      if (_pendingSelectionSets.Count > 0)
        MapPendingSelectionSubsets(); 
    }

    private void MapPendingSelectionSubsets() {
      while (_pendingSelectionSets.Count > 0) {
        if (_requestContext.Failed)
          return;
        var oldSets = _pendingSelectionSets;
        _pendingSelectionSets = new List<PendingSelectionSet>();
        foreach (var selSet in oldSets)
          MapSelectionSubSet(selSet.SubSet, selSet.OverType, selSet.Directives);
      }// while
    }

    private void MapSelectionSubSet(SelectionSubset selSubset, TypeDefBase typeDef, IList<RequestDirective> directives) {
      switch(typeDef) {
        case ScalarTypeDef _:
        case EnumTypeDef _:
          // that should never happen
          AddError($"Scalar or Enum may not have a selection subset", selSubset);
          break;

        case ObjectTypeDef objTypeDef:
          MapObjectSelectionSubset(selSubset, objTypeDef, directives);
          break;

        case InterfaceTypeDef intTypeDef:
          foreach(var objType in intTypeDef.PossibleTypes)
            MapObjectSelectionSubset(selSubset, objType, directives);
          break;

        case UnionTypeDef unionTypeDef:
          foreach(var objType in unionTypeDef.PossibleTypes)
            MapObjectSelectionSubset(selSubset, objType, directives, isForUnion: true);
          break;
      }
    }

    
    // Might be called for ObjectType or Interface (for intf - just to check fields exist)
    private void MapObjectSelectionSubset(SelectionSubset selSubset, ObjectTypeDef objectTypeDef, IList<RequestDirective> directives, bool isForUnion = false) {
      var mappedItems = new List<MappedSelectionItem>();
      foreach(var item in selSubset.Items) {

        switch(item) {
          case SelectionField selFld:
            var fldDef = objectTypeDef.Fields.FirstOrDefault(f => f.Name == selFld.Name);
            if(fldDef == null) {
              // if field not found, the behavior depends if it is a union; it is error for a union
              if(!isForUnion)
                AddError($"Field '{selFld.Name}' not found on type '{objectTypeDef.Name}'.", selFld);
              continue; 
            }
            var mappedArgs = MapArguments(selFld.Args, fldDef.Args, selFld);
            var mappedFld = new MappedSelectionField(selFld, fldDef, mappedItems.Count, mappedArgs);
            mappedItems.Add(mappedFld);
            AddRuntimeModelDirectives(mappedFld);
            AddRuntimeRequestDirectives(mappedFld); 
            ValidateMappedFieldAndProcessSubset(mappedFld);
            break;

          case FragmentSpread fs:
            var mappedSpread = MapFragmentSpread(fs, objectTypeDef, isForUnion);
            if (mappedSpread != null) {// null is indicator of error
              AddRuntimeRequestDirectives(mappedSpread);
              mappedItems.Add(mappedSpread);
            }
            break;
        }//switch

      } //foreach item

      selSubset.MappedSubSets.Add(new MappedSelectionSubSet() { ObjectTypeDef = objectTypeDef, MappedItems = mappedItems });
    }

    private MappedFragmentSpread MapFragmentSpread(FragmentSpread fs, ObjectTypeDef objectTypeDef, bool isForUnion) {
      // if it is not inline fragment, it might need to map to FragmentDef; inline fragments are auto-mapped at construction
      if (fs.Fragment == null)
        fs.Fragment = GetFragmentDef(fs.Name);
      if (fs.Fragment == null) {
        AddError($"Fragment {fs.Name} not defined.", fs);
        return null;
      }
      // inline fragments are mapped in-place, here.
      // we need to map them here, once we know the target type
      if (fs.IsInline) {
        var onTypeRef = fs.Fragment.OnTypeRef;
        var skip = onTypeRef != null && onTypeRef.TypeDef != objectTypeDef;
        if (skip)
          return null; 
        MapObjectSelectionSubset(fs.Fragment.SelectionSubset, objectTypeDef, fs.Directives, isForUnion);
      }

      // there must be mapped field set now
      var mappedFragmItemSet = fs.Fragment.SelectionSubset.GetMappedSubSet(objectTypeDef.ClrType);
      if (mappedFragmItemSet == null) {
        AddError($"FATAL: Could not retrieve mapped item list for fragment spread {fs.Name}", fs, ErrorCodes.ServerError);
        return null;
      }
      var mappedSpread = new MappedFragmentSpread(fs, mappedFragmItemSet.MappedItems);
      return mappedSpread;
    }

    private void ValidateMappedFieldAndProcessSubset(MappedSelectionField mappedField) {
      var typeDef = mappedField.FieldDef.TypeRef.TypeDef;
      var selField = mappedField.Field;
      var selSubset = selField.SelectionSubset;
      var typeName = typeDef.Name; 
      switch(typeDef) {
        case ScalarTypeDef _:
        case EnumTypeDef _:
          if (selSubset != null)
            AddError($"Field '{selField.Key}' of type '{typeName}' may not have a selection subset.", selSubset);
          break;
        
        default: // ObjectType, Union or Interface 
          if (selSubset == null) {
            AddError($"Field '{selField.Key}' of type '{typeName}' must have a selection subset.", selField);
            return; 
          }
          _pendingSelectionSets.Add(new PendingSelectionSet() {
            SubSet = selSubset, OverType = typeDef, Directives = selField.Directives
          });
          break;
      }
    }


    */

  } // class
}