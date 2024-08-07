﻿using System;
using System.Collections.Generic;
using System.Text;
using NGraphQL.Utilities;
using System.Text.Json;

namespace NGraphQL.Json;

public static class SerializationHelper {

  static JsonSerializerOptions _jsonOptions = JsonDefaults.JsonOptions;

  public static object ConvertTo(object obj, Type type) {
    if (obj == null)
      return default;
    var json = JsonSerializer.Serialize(obj, _jsonOptions);
    var clone = JsonSerializer.Deserialize(json, type, _jsonOptions);
    return clone;
  }

  public static string Serialize(object obj) {
    var json = JsonSerializer.Serialize(obj, _jsonOptions);
    return json;
  }

  public static T Deserialize<T>(string json) {
    var obj = JsonSerializer.Deserialize<T>(json, _jsonOptions);
    return obj;
  }
  public static object Deserialize(string json, Type type) {
    var obj = JsonSerializer.Deserialize(json, type, _jsonOptions);
    return obj;
  }

  /// <summary>Deserializes object leaving object fields as JsonElement, 
  /// to be later deserialized as a specific type. </summary>
  /// <typeparam name="T">Type</typeparam>
  /// <param name="json">Json content</param>
  /// <returns>Deserialized object.</returns>
  public static T DeserializePartial<T>(string json) {
    var obj = JsonSerializer.Deserialize<T>(json, JsonDefaults.JsonOptionsPartial);
    return obj;
  }


}
