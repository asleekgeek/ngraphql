﻿using NGraphQL.Core;
using NGraphQL.Introspection;
using NGraphQL.Model;
using NGraphQL.Server.Execution;
using NGraphQL.Server.RequestModel;

namespace NGraphQL.Core {

  [HandlesDirective("@include")]
  public class IncludeDirectiveHandler: IDirectiveHandler, ISkipDirectiveAction {
    public bool ShouldSkip(RequestContext context, MappedSelectionItem item, object[] argValues) {
      var boolArg = (bool)argValues[0];
      return !boolArg;
    }
  }

}