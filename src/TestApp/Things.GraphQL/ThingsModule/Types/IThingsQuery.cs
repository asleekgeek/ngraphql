﻿using System;
using System.Collections.Generic;
using System.Text;
using NGraphQL.CodeFirst;

namespace Things.GraphQL.Types {

  public interface IThingsQuery {
    // Resolver is linked thru attribute on resolver method
    List<Thing_> Things { get; }

    [Resolver(nameof(ThingsResolvers.GetThings))]
    List<ThingX_> ThingsX { get; }

    /// <summary>Returns the Thing specified by Id.</summary>
    /// <param name="id">Thing id.</param>
    Thing_ GetThing(int id);

    /// <summary>Returns the Thing with invalid value(s).</summary>
    Thing_ GetInvalidThing();

    int WaitForPositiveValueAsync();

    TheFlags GetFlags();

    string EchoInputValues(bool boolVal, int intVal, float floatVal, string strVal, ThingKind kindVal);

    string EchoInputValuesWithNulls(bool? boolVal, long? longVal, double? doubleVal, [Null] string strVal,
       [DeprecatedDir("KindVal is deprecated")] ThingKind? kindVal, // test of @deprecated directive
       TheFlags? flags);

    string EchoIntArray(int[] intVals);

    string EchoEnumArray(TheFlags? flagVals);

    Dictionary<string, object> EchoInputObjWithMap(InputObjWithMap inp);

    // the following 2 fields are matched to resolvers in 2 different ways. 
    //  the first field uses [Resolver(methodName)] attribute on the field (in GraphQLQ Query);
    //  the other one is using [ResolvesField(fieldName)] attribute on the resolver method. 
    //  Using attribute on resolver method allows you to keep GraphQL type(s) free of 
    // references to resolver classes and any dependency on deep server-side app logic. 
    [Resolver("EchoInputObject")]
    string EchoInputObj(InputObj inpObj);

    // matched with resolver using [ResolvesField(field)] attribute on resolver method
    string EchoInputObjWithEnums(InputObjWithEnums inpObj);

    string EchoCustomScalars(decimal dec, Guid uuid);

    string EchoDateTimeScalars(DateTime dt, DateTime date, TimeSpan time);

    int[][] GetIntListRank2();

    string EchoIntListRank2(int[][] values);

    Thing_[] GetThingsList();

    Thing_[][] GetThingsListRank2();

    // test of sending, receiving Flag sets
    TheFlags EchoFlags(TheFlags? flags);

    string EchoFlagsStr(TheFlags? flags);

    IList<ThingsUnion> GetThingsUnionList();

    IList<INamedObj> GetSomeNamedObjects();

    ThingKind[] GetAllKinds();

    /// <summary>Demonstrates converting custom exc into GraphQL errors in the response. 
    /// Throws AggregateException; the special handler on error event catches it, unpacks the exc,
    /// and posts messages from child exceptions as separate GraphQL errors.  
    /// </summary>
    /// <returns>Throws exc.</returns>
    int ThrowAggrExc();

    decimal DecTimesTwo(decimal dec); 
  }
}