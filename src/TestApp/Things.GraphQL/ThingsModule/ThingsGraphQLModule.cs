﻿using System;
using System.Collections.Generic;
using System.Text;
using NGraphQL.CodeFirst;

using Things.GraphQL.Types;

namespace Things.GraphQL {

  // Api definitions (types, resolvers) are first orginized into api modules,
  // modules then  assembled into a GraphQLApi

  public class ThingsGraphQLModule : GraphQLModule {
    public ThingsGraphQLModule() {
      // 1. Register all types
      base.EnumTypes.Add(typeof(ThingKind), typeof(TheFlags));
      base.ObjectTypes.Add(typeof(Thing), typeof(OtherThing),
             typeof(ThingImplInterface), typeof(OtherThingWrapper), typeof(ThingX));
      //Note: NGraphQL allows to use input types as output (Object) types
      base.InputTypes.Add(typeof(InputObj), typeof(InputObjWithNulls), typeof(InputObjWithEnums), typeof(InputObjParent),         
        typeof(InputObjChild),  typeof(InputObjWithList), typeof(InputObjWithCustomScalars));
      base.InterfaceTypes.Add(typeof(INamedObj), typeof(IObjWithId), typeof(IIdNameIntf), typeof(IIdNameOtherThingsIntf));
      base.UnionTypes.Add(typeof(ThingsUnion));

      base.QueryType = typeof(IThingsQuery);
      base.MutationType = typeof(IThingsMutation);
      base.SubscriptionType = typeof(IThingsSubscription);

      // Define mappings of entities (biz app objects) to API Object Types 
      MapEntity<ThingEntity>().To<Thing>(th => new Thing() {
        Id = th.Id,
        Name = th.Name,
        StrField = th.Name + "-EXT",
        Description = th.Descr,
        Kind = th.TheKind,
        TheFlags = th.Flags,
        DateTimeOpt = th.DateQ,
        SomeDateTime = th.SomeDate,
        // example of using FromMap function to explicitly convert biz object to API object (BizThing => ApiThing)
        // Note: we could skip this, as field names match, it would automap
        NextThing = FromMap<Thing>(th.NextThing), 
        OtherThingWrapped = CreateOtherThingWrapper(th.MainOtherThing),        
      });

      // map ThingEntity to another GrqphQL type ThingX
      MapEntity<ThingEntity>().To<ThingX>(th => new ThingX() {
        IdX = th.Id,
        NameX = th.Name,
        KindX = th.TheKind,
      });


      MapEntity<OtherThingEntity>().To<OtherThing>(); // engine will automatically map all matching fields
      MapEntity<IExtCustomInterface>().To<ThingImplInterface>();

      // testing hide-enum-value feature. Use this if you have no control over enum declaration, but you want to 
      //  remove/hide some members; for ex, some flag enums declare extra flag combinations as enum members (I do this often),
      //  this practice does not fit with GraphQL semantics, so these values should be removed from the GraphQL enum declaration/schema. 
      this.IgnoreMember(typeof(ThingKind), nameof(ThingKind.KindFour_Ignored));

      // Resolvers
      this.ResolverClasses.Add(typeof(ThingsResolvers));

    }// constructor

    // testing bug fix
    private static OtherThingWrapper CreateOtherThingWrapper(OtherThingEntity otherTh) {
      if (otherTh == null)
        return null;
      return new OtherThingWrapper() {
        OtherThingName = otherTh.Name, WrappedOn = DateTime.Now,
        OtherThing = new OtherThing() { IdStr = otherTh.IdStr, Name = otherTh.Name }
      };
    }

  } // class


}
