﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using NGraphQL.CodeFirst;
using NGraphQL.Json;
using NGraphQL.Server;
using NGraphQL.Server.Execution;
using NGraphQL.Utilities;

using Things;
using Things.GraphQL;

namespace NGraphQL.Tests {

  public static class TestEnv {

    public static ThingsGraphQLServer ThingsServer;
    public static bool LogEnabled = true;
    public static string LogFilePath = "_graphQLtests.log";

    public static RequestContext LastRequestContext;

    public static void Init() {
      if(ThingsServer != null)
        return;
      if(File.Exists(LogFilePath))
        File.Delete(LogFilePath);
      try {
        var thingsBizApp = new ThingsApp();
        // By default now server ignores when a resolver for non-null field returns null.
        // We have explicitly turn it off, to make server detect it and throw error - we have a special test for this
        var options = GraphQLServerOptions.DefaultDev & ~GraphQLServerOptions.IgnoreOutNullFaults;
        var stt = new GraphQLServerSettings() { Options = options };
        ThingsServer = new ThingsGraphQLServer(thingsBizApp, stt);
        // Add logging hook
        ThingsServer.Events.RequestCompleted += ThingsServer_RequestCompleted;
        ThingsServer.Initialize();
      } catch (ServerStartupException sEx) {
        LogText(sEx.ToText() + Environment.NewLine);
        LogText(sEx.GetErrorsAsText());
        throw;
      }
      // Write schema doc to file
      var schemaDoc = ThingsServer.Model.SchemaDoc;
      File.WriteAllText("_thingsApiSchema.txt", schemaDoc);
    }


    private static void ThingsServer_RequestCompleted(object sender, GraphQLServerEventArgs e) {
      LastRequestContext = e.RequestContext;
      LogCompletedRequest(e.RequestContext);
    }

    public static void LogTestMethodStart([CallerMemberName] string testName = null) {
      LogText($@"

==================================== Test Method {testName} ================================================
");
    }

    public static void LogTestDescr(string descr) {
      LogText($@"
Testing: {descr}
");
    }

    public static void LogCompletedRequest(RequestContext context) {
      if(!LogEnabled)
        return;
      var mx = context.Metrics;
      var jsonRequest = SerializationHelper.Serialize(context.RawRequest);
      // for better readability, unescape \r\n
      jsonRequest = jsonRequest.Replace("\\r\\n", Environment.NewLine);
      var jsonResponse = SerializeResponse(context.Response);
      var text = $@"
Request: 
{jsonRequest}

Response:
{jsonResponse}

// execution time: {mx.Duration.TotalMilliseconds} ms, request from cache: {mx.FromCache}, threads: {mx.ExecutionThreadCount}, " + 
$@" resolver calls: {mx.ResolverCallCount}, output objects: {mx.OutputObjectCount}
----------------------------------------------------------------------------------------------------------------------------------- 

";
      LogText(text);
      foreach(var ex in context.Exceptions)
        LogText(ex.ToText());
    }

    public static void LogText(string text) {
      File.AppendAllText(LogFilePath, text);
    }


    public static void LogException(string query, Exception ex) {
      var text = $@"

!!! Exception !!! ----------------------------------------------------------------      
{ex}

Failed request:
{query}
";
      File.AppendAllText(LogFilePath, text);
    }

    public static async Task<GraphQLResponse> ExecuteAsync(string query, IDictionary<string, object> variables = null,bool throwOnError = true) {
      GraphQLResponse resp = null;       
      try {
        var req = new GraphQLRequest() { Query = query, Variables = variables };
        resp = await ThingsServer.ExecuteAsync(req);
        if (!resp.IsSuccess()) {
          var errText = resp.GetErrorsAsText();
          Debug.WriteLine("Errors: \r\n" + errText);
        }
      } catch(Exception ex) {
        TestEnv.LogException(query, ex);
        throw;
      }
      if (resp != null && resp.Errors != null && resp.Errors.Count > 0 && throwOnError)
        throw new Exception($"Request failed: {resp.Errors[0].Message}");
      return resp;
    }

    // Serialization for logging 
    private static string SerializeResponse(GraphQLResponse response) {
      try {
        if (response.Errors.Count > 0)
          return SerializationHelper.Serialize(response);
        else
          return SerializationHelper.Serialize(new { response.Data });
      } catch (Exception ex) {
        var errText = "FATAL: " + ex.ToString();
        LogText(errText);
        return errText; 
      }
    }

  }
}
