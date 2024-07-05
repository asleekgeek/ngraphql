﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NGraphQL.Json;
using NGraphQL.Server.AspNetCore;
using NGraphQL.Subscriptions;

namespace NGraphQL.Tests.HttpTests;
using TVars = Dictionary<string, object>;

[TestClass]
public class SubscriptionTests {

  [TestInitialize]
  public void Init() {
    TestEnv.Initialize();
  }

  [TestMethod]
  public async Task TestSubscriptions() {
    TestEnv.LogTestMethodStart();
    TestEnv.LogTestDescr(@"  Simple subscription test");
    var messages = new List<string>();

    // setup SignalR client
    var hubUrl = TestEnv.ServiceUrl + "/subscriptions";
    var hubConn = new HubConnectionBuilder().WithUrl(hubUrl).Build();
    hubConn.On<string>(SignalRSender.ClientReceiveMethod, 
      (msg) => { 
        messages.Add(msg);  
    });
    await hubConn.StartAsync();

    // 1. AddSubscription to ThingUpdates
    var thingId = 1;
    var subscribeMsg = new SubscribeMessage() {
      Id = "ThingUpdate/1/" + Guid.NewGuid(),
      Type = SubscriptionMessageTypes.Subscribe,
      Payload = new SubscribePayload() {
        OperationName = null,
        Query = @"
subscription($thingId: Int) {
  subscribeToThingUpdates(thingId: $thingId) {
     id name kind 
  }
}",
        Variables = new Dictionary<string, object>() { { "thingId", thingId } },
       }
    };

    var msgJson = SerializationHelper.Serialize(subscribeMsg);
    var serverMethod = SignalRListener.ServerReceiveMethod;
    await hubConn.SendAsync(serverMethod, msgJson);

    // make Thing update through mutation
    await MutateThing(1, "newName");

    // make multiple delays (for thread yields)
    await WaitYield(); 
    Assert.AreEqual(1, messages.Count, "Expected messages");

  }// method

  private async Task WaitYield() {
    for (int i = 0; i < 10; i++) {
      Thread.Yield();
      await Task.Delay(50);
    }
  }

  private async Task MutateThing(int thingId, string newName) {
    var mutReq = @"
mutation myMut($thingId: Int, $newName: String) { 
  mutateThing(id: $thingId, newName: $newName) { 
    id 
    name 
  }
}";
    var vars = new TVars() { { "thingId", thingId }, { "newName", newName } };
    await TestEnv.Client.PostAsync(mutReq, vars);
  }

}
