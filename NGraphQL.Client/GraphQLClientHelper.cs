﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace NGraphQL.Client {
  using IDict = IDictionary<string, object>;

  public static class GraphQLClientHelper {

    public static void EnsureNoErrors(this ServerResponse response) {
      if (response.Errors != null && response.Errors.Count > 0)
        throw new Exception("GraphQL request failed.");
    }

  }
}
