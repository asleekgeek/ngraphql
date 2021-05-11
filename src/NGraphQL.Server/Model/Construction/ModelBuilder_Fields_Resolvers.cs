﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NGraphQL.CodeFirst;
using NGraphQL.Model;
using NGraphQL.Introspection;
using NGraphQL.Utilities;

namespace NGraphQL.Model.Construction {

  public partial class ModelBuilder {

    // map resolvers having [ResolvesField] attribute
    private void MapResolversByResolvesFieldAttribute() {
      // go thru resolver classes, find methods with ResolvesField attr
      foreach(var resInfo in _allResolvers) {
        var resAttr = resInfo.ResolvesAttribute; 
        if (resAttr == null)
          continue;
        var fieldName = resAttr.FieldName.FirstLower();
        // check target type
        if (resAttr.TargetType != null) {
          if(!_model.TypesByClrType.TryGetValue(resAttr.TargetType, out var typeDef) || !(typeDef is ObjectTypeDef objTypeDef)) {
            AddError($"Resolver method '{resInfo}': target type '{resAttr.TargetType}' not registered or is not Object type.");
            continue; 
          }
          // match field
          var fld = objTypeDef.Fields.FirstOrDefault(f => f.Name == fieldName);
          if (fld == null) {
            AddError($"Resolver method '{resInfo}': target field '{fieldName}' not found "
                      + $"on type '{resAttr.TargetType}'.");
            continue;
          }
          SetupFieldResolverMethod(objTypeDef, fld, resInfo, resAttr);
          continue; 
        } // if TargetType != null
        // TargetType is null - find match by name only
        var fields = _typesToMapFields.SelectMany(t => t.Fields).Where(f => f.Name == fieldName).ToList(); 
        switch(fields.Count) {
          case 1:
            var fld = fields[0];
            SetupFieldResolverMethod((ObjectTypeDef) fld.OwnerType, fld, resInfo, resAttr);
            break;

          case 0:
            AddError($"Resolver method '{resInfo}': target field '{fieldName}' not found "
                      + $"on any object type.");
            break;
            
          default:
            AddError($"Resolver method '{resInfo}': multipe target fields '{fieldName}' "
                      + $"found on Object types.");
            break; 
        }
      } //foreach resMeth
    }

    private void MapFieldsToResolversByResolverAttribute() {
      foreach (var typeDef in _typesToMapFields) {
        foreach (var field in typeDef.Fields) {
          if (field.ClrMember == null)
            continue; //__typename has no clr member
          var resAttr = GetAllAttributesAndAdjustments(field.ClrMember).Find<ResolverAttribute>();
          if (resAttr == null)
            continue; 
          var resolverType = resAttr.ResolverClass;
          if (resolverType != null) {
            if (!typeDef.Module.ResolverClasses.Contains(resolverType)) {
              AddError($"Field {typeDef.Name}.{field.Name}: target resolver class {resolverType.Name} is not registered with module. ");
              continue;
            }
          }
          // 
          var methName = resAttr.MethodName ?? field.ClrMember.Name;
          List<MethodInfo> methods;
          if (resolverType != null) {
            methods = resolverType.GetResolverMethods(methName);
            // with explicit resolver, if method not found - it is error
            if (methods.Count == 0) {
              AddError($"Field {typeDef.Name}.{field.Name}: failed to match resolver method; target resolver class {resolverType.Name}.");
              continue;
            }
          } else {
            // targetResolver is null
            methods = new List<MethodInfo>();
            foreach (var resType in typeDef.Module.ResolverClasses) {
              var mlist = resType.GetResolverMethods(methName);
              methods.AddRange(mlist);
            }
          }
          switch (methods.Count) {
            case 0:
              AddError($"Field {typeDef.Name}.{field.Name}: failed to find resolver method {methName}. ");
              break;

            case 1:
              SetupFieldResolverMethod(typeDef, field, methods[0], resAttr);
              break;

            default:
              AddError($"Field {typeDef.Name}.{field.Name}: found more than one resolver method ({methName}).");
              break; 
          }
        } //foreach field
      } //foreach typeDef
    }//method

    private void MapFieldsToResolversByName() {
      foreach (var typeDef in _typesToMapFields) {
        // get all resolver methods from the same module
        foreach (var field in typeDef.Fields) {
          if (field.ExecutionType != ResolverKind.NotSet)
            continue;
          if (field.ClrMember == null)
            continue; //__typename has no clr member
          var methName = field.ClrMember.Name;
          var methods = _allResolvers.Where(res => res.Method.Name == methName).ToList();
          switch(methods.Count) {
            case 0: continue;
            case 1:
              SetupFieldResolverMethod(typeDef, field, methods[0], null);
              continue;
            default:
              AddError($"Field {typeDef.Name}.{field.Name}: found more than one resolver method ({methName}).");
              continue; 
          } //switch
        } //foreach field
      } //foreach typeDef
    }//method

    private bool SetupFieldResolverMethod(ObjectTypeDef typeDef, FieldDef field, ResolverMethodInfo resolverInfo, Attribute sourceAttr) {
      var retType = resolverInfo.ReturnType;
      // validate return type
      if (!CheckReturnTypeCompatible(retType, field, resolverInfo.Method))
        return false;

      field.Resolver = resolverInfo;
      if (resolverInfo.ReturnsTask)
        field.Flags |= FieldFlags.ResolverReturnsTask;
      if (typeDef is ObjectTypeDef otd && otd.TypeRole == TypeRole.Data)
        field.Flags |= FieldFlags.HasParentArg;
      field.ExecutionType = ResolverKind.Method;
      ValidateResolverMethodArguments(typeDef, field); 
      return !_model.HasErrors;
    }

    private bool ValidateResolverMethodArguments(ComplexTypeDef typeDef, FieldDef fieldDef) {
      var resMethod = fieldDef.Resolver.Method; 
      // Check first parameter - must be IFieldContext
      var prms = resMethod.GetParameters();
      if (prms.Length == 0 || prms[0].ParameterType != typeof(IFieldContext)) {
        AddError($"Resolver method {resMethod.GetFullRef()}: the first parameter must be of type '{nameof(IFieldContext)}'.");
        return false;
      }

      // compare list of field parameters with list of resolver method parameters; 
      //  resolver method has extra FieldContext and Parent parameters
      var argCountDiff = 1;
      if (fieldDef.Flags.IsSet(FieldFlags.HasParentArg))
        argCountDiff = 2;
      var expectedPrmCount = fieldDef.Args.Count + argCountDiff;
      if (expectedPrmCount != prms.Length) {
        AddError($"Resolver method {resMethod.GetFullRef()}: parameter count mismatch with field arguments, expected {expectedPrmCount}, " + 
           "with added IFieldContext and possibly Parent object parameter. ");
        return false; 
      }
      // parameter names/types must be identical
      for(int i = argCountDiff; i < prms.Length; i++) {
        var prm = prms[i];
        var arg = fieldDef.Args[i - argCountDiff];
        if (prm.Name != arg.Name || prm.ParameterType != arg.ParamType) {
          AddError($"Resolver method {resMethod.GetFullRef()}: parameter name/type mismatch with field argument; parameter: {prm.Name}.");
          return false; 
        }
      }
      return true;
    }

    private void VerifyListParameterType(Type type, MethodBase method, string paramName) {
      if (!type.IsArray && !type.IsInterface)
        AddError($"Method {method.GetFullRef()}: Invalid list parameter type - must be array or IList<T>; parameter {paramName}. ");
    }

    private bool CheckReturnTypeCompatible(Type returnType, FieldDef field, MethodInfo method) {
      UnwrapClrType(returnType, method, out var retBaseType, out var kinds, null);
      var retTypeRank = kinds.GetListRank();
      var fldTypeRef = field.TypeRef; 
      var fldTypeRank = fldTypeRef.Rank;
      if (field.TypeRef.TypeDef.IsEnumFlagArray())
        fldTypeRank--;
      if (retTypeRank != fldTypeRank) {
        AddError($"Resolver method {method.GetFullRef()}: return type {returnType.Name} (rank {retTypeRank}) is not compatible with type " + 
                 $" {field.TypeRef.Name} of  field '{field.Name}'; list rank mismatch.");
        return false; 
      }
      var withBaseType = fldTypeRef.TypeDef.ClrType; 
      switch (fldTypeRef.TypeDef) {
        case ScalarTypeDef _:
        case EnumTypeDef _:
          if(retBaseType != withBaseType) {
            AddError($"Resolver method {method.GetFullRef()}: return type is incompatible with type {fldTypeRef.Name} of  field '{field.Name}'.");
            return false; 
          }
          return true;

        case ObjectTypeDef objTypeDef:
          var mappedTypeDef = _model.GetMappedGraphQLType(retBaseType);
          if (mappedTypeDef != objTypeDef) {
            AddError($"Resolver method {method.GetFullRef()}: return type is incompatible with field type {fldTypeRef.Name}");
            return false;
          }
          return true;

        case UnionTypeDef _:
        case InterfaceTypeDef _:
          //TODO: implement later
          return true; 
      }
      return true;  
    }
  }
}
