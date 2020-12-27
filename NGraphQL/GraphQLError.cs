﻿using System;
using System.Collections.Generic;
using NGraphQL.CodeFirst;

namespace NGraphQL {

  // see https://spec.graphql.org/June2018/#sec-Errors
  public class GraphQLError {
    public const string ErrorCodeKey = "code";
    public string Message;
    public IList<QueryLocation> Locations = new List<QueryLocation>();
    public IList<object> Path;
    public IDictionary<string, object> Extensions = new Dictionary<string, object>(); 

    public GraphQLError() { }

    public GraphQLError(string message, IList<object> path = null, QueryLocation location = null, string type = null) {
      Message = message;
      Path = path ?? Array.Empty<object>();
      if (location != null)
        Locations.Add(location);
      if (!string.IsNullOrEmpty(type))
        Extensions[ErrorCodeKey] = type;
    }

    public override string ToString() {
      var str = Message;
      if (Path != null)
        str += " path: [" + string.Join(", ", Path) + "]";
      if (Locations != null && Locations.Count > 0)
        str += $" at: {Locations[0]}";
      return str;
    }
  }

  public class QueryLocation {
    public int Line;
    public int Column;

    public static readonly QueryLocation StartLocation = new QueryLocation() { Line = 1, Column = 1 };
    public override string ToString() => $"({Line}, {Column})";
  }

}