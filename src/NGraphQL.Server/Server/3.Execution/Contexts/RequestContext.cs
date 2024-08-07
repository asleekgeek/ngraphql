﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;

using NGraphQL.CodeFirst;
using NGraphQL.Model;
using NGraphQL.Model.Request;
using NGraphQL.Subscriptions;
using NGraphQL.Server.Subscriptions;
using NGraphQL.Utilities;

namespace NGraphQL.Server.Execution {

  public class RequestContext : IRequestContext {
    public GraphQLServer Server { get; }
    public object App => Server.App;
    public ClaimsPrincipal User { get; set; }
    public GraphQLApiModel ApiModel => Server.Model;
    public IServiceProvider HostServices { get; private set; }

    public GraphQLRequest RawRequest;
    public ParsedGraphQLRequest ParsedRequest;
    public GraphQLOperation Operation { get; internal set; }
    public IList<VariableValue> OperationVariables { get; } = new List<VariableValue>();
    
    public SubscriptionContext Subscription;

    // contexts for all directives in the ParsedRequest
    public IList<DirectiveContext> DirectiveContexts = new List<DirectiveContext>();
    public RequestMetrics Metrics { get; } = new RequestMetrics();
    public RequestQuota Quota;

    public GraphQLResponse Response { get; } = new GraphQLResponse();
    /// <summary>Dictionary for use by resolvers to pass data around. </summary>
    public IDictionary<string, object> CustomData { get; } = new ConcurrentDictionary<string, object>();
    public object VitaOperationContext { get; set; }

    public CancellationToken CancellationToken => _cancellationTokenSource.Token;
    private CancellationTokenSource _cancellationTokenSource;
    private CancellationToken _externalCancellationToken;


    internal long StartTimestamp;
    public IList<Exception> Exceptions = new List<Exception>();
    public bool Failed { get; internal set; }
    public readonly object Lock = new object();
    // see comment on GetCreateDerivedTypeRef method for more info
    internal readonly IList<TypeRef> CustomTypeRefs = new List<TypeRef>();
    // used by http server
    public object HttpContext { get; }
    // For Http server, original variables
    public IDictionary<string, object> RawVariables;


    public RequestContext(GraphQLServer server, GraphQLRequest rawRequest, CancellationToken cancellationToken = default,
                          ClaimsPrincipal user = null, RequestQuota quota = null, 
                          IServiceProvider hostServices = null, object httpContext = null) {
      Server = server;
      RawRequest = rawRequest;
      _externalCancellationToken = cancellationToken;
      User = user;
      HostServices = hostServices;
      HttpContext = httpContext;
      Quota = quota ?? Server.DefaultRequestQuota ?? new RequestQuota();
      StartTimestamp = AppTime.GetTimestamp();
      // cancellation tokens, initial implementation of limiting request time by quota
      _cancellationTokenSource = new CancellationTokenSource(Quota.MaxRequestTime);
      _cancellationTokenSource.Token.Register(OnCancelled);
      if (_externalCancellationToken != default)
        _externalCancellationToken.Register(OnExternalTokenCancelled);
    }
    
    // special case for re-executing request for subscriptions, just to grab output tree
    public RequestContext(GraphQLServer server, ParsedGraphQLRequest parsedRequest, 
          SubscriptionContext subscriptionContext) {
      Server = server;
      ParsedRequest = parsedRequest;
      Subscription = subscriptionContext;
      //var topScope = new OutputObjectScope(new RequestPath(), null, null);
      //this.Response.Data = topScope;
      Quota = Server.DefaultRequestQuota ?? new RequestQuota();
      _cancellationTokenSource = new CancellationTokenSource(Quota.MaxRequestTime);
      _cancellationTokenSource.Token.Register(OnCancelled);
    }

    bool _externalCancelled;
    private void OnExternalTokenCancelled() {
      _externalCancelled = true; 
      _cancellationTokenSource.Cancel(); 
    }
    private void OnCancelled() {
      if (!_externalCancelled) {
        // we assume it's timeout, token is already signaling; add error
        var err = new GraphQLError(
          $"Request canceled, request time exceeded max specified by quota ({Quota.MaxOutputObjects}).",  type: "Quota");
        AddError(err);
      }
    }

    public void AddError(string message, RequestObjectBase item, string errorType = ErrorCodes.BadRequest) {
      var path = item.GetRequestObjectPath();
      var err = new GraphQLError(message, path, item.SourceLocation, errorType);
      AddError(err);
    }

    public void AddError(GraphQLError error, Exception sourceException = null) {
      lock (this.Lock) {
        this.Failed = true;
        var errors = this.Response.Errors;
        errors.Add(error);
        if (sourceException != null)
          this.Exceptions.Add(sourceException); 
        if (errors.Count > RequestQuota.MaxErrors) {
          errors.Add(new GraphQLError("Too many errors, request aborted."));
          throw new AbortRequestException();
        }
      }
    }


  }

  /* About custom type refs in RequestContext
    // when parsing the request, we try to lookup existing typeRef registered with TypeDef in the model; 
    // we might not find it; for ex - we are looking for type [[int]]!, but Model does not have any field or resolver arg
    // of this type. Model might have [[int]] type, and this is a legit case. We can create this new TypeRef,
    // but we cannot register it in typeDef's list - the model is read-only now.
    // So instead we add it request's CustomTypeRefs list. 
  */

}
