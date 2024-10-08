﻿using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NGraphQL.Subscriptions;
using Things;

namespace NGraphQL.Tests.HttpTests;
using TVars = Dictionary<string, object>;

[TestClass]
public class SubscriptionTests {

  [TestInitialize]
  public void Init() {
    TestEnv.Initialize();
  }

  public class ThingUpdate {
    public int Id;
    public string Name;
    public ThingKind Kind; 
  }

  [TestMethod]
  public async Task TestSubscriptions() {
    TestEnv.LogTestMethodStart();
    TestEnv.LogTestDescr(@"  Simple subscription test");
    var client = TestEnv.Client;
    var updates = new List<ThingUpdate>();
    // listen to all messages, and error messages
    var messages = new List<SubscriptionMessage>();
    client.MessageReceived += (s, args) => {
      messages.Add(args.Message);
    };
    // listen to all received errors
    var errors = new List<ErrorMessage>();
    client.ErrorReceived += (s, args) => {
      errors.Add(args.Message);
    };

    const string subscribeRequest = @"
subscription($thingId: Int) {
  subscribeToThingUpdates(thingId: $thingId) {
     id name kind 
  }
}";

    // 1. Subscribe to updates of Thing #1 and #2
    var sub4 = await client.Subscribe<ThingUpdate>(subscribeRequest, new TVars() { { "thingId", 4 } }, (clientSub, msg) => {
      updates.Add(msg);
    });
    var sub5 = await client.Subscribe<ThingUpdate>(subscribeRequest, new TVars() { { "thingId", 5 } }, (clientSub, msg) => {
      updates.Add(msg);
    });
    WaitYield();

    // 2.Make Thing update through mutation
    await MutateThing(4, "newName_4A");
    await MutateThing(4, "newName_4B");
    await MutateThing(5, "newName_5A");
    await MutateThing(6, "newName_6A"); //this will not come, we are not subscribed to #3
    WaitYield();

    // 3. Check notifications pushed by the server
    Assert.AreEqual(3, updates.Count, "Expected 3 total notifications");
    var updates4 = updates.Where(u => u.Id == 4).ToList();
    var updates5 = updates.Where(u => u.Id == 5).ToList();
    Assert.AreEqual(2, updates4.Count, "Expected 2 updates for Thing 4");
    Assert.AreEqual(1, updates5.Count, "Expected 1 update for Thing 5");

    // 4. Unsubsribe sub4, we should no longer see updates for Thing/4 
    updates.Clear();
    await client.Unsubscribe(sub4);
    WaitYield();
    await MutateThing(4, "newName_1F");
    WaitYield();
    Assert.AreEqual(0, updates.Count, "No updates expected after unsubscribe for Thing 4");

    // Ping server, client should receive pong message 
    messages.Clear();
    await client.PingServer();
    WaitYield();
    Assert.AreEqual(1, messages.Count, "Expected 1 pong message");
    Assert.AreEqual(SubscriptionMessageTypes.Pong, messages[0].Type, "Expected 'pong' message.");

    // Checking error handling
    // if Subscribe fails, client can see error either thru global client.ErrorReceived event, 
    //   or per subscription callback.
    // Check syntax error first
    var badSubErrors = new List<ErrorMessage>();
    var badSubRequest = "ABCD " + subscribeRequest;
    var errSubsrc = await client.Subscribe<ThingUpdate>(badSubRequest, null, (c, p) => { },
         (c, err) => {
           badSubErrors.Add(err);
         }
         );
    WaitYield();
    Assert.AreEqual(1, badSubErrors.Count, "Expected subscription error");
    Assert.AreEqual(1, errors.Count, "Expected 1 global error");

    // Two subscriptions in one call - not allowed
    badSubErrors.Clear(); 
    badSubRequest = @"
subscription {
  sub1: subscribeToThingUpdates(thingId: 1) {
     id name kind 
  }
  sub2: subscribeToThingUpdates(thingId: 2) {
     id name kind 
  }
}";
    errSubsrc = await client.Subscribe<ThingUpdate>(badSubRequest, null, (c, p) => { },
         (c, err) => {
           badSubErrors.Add(err);
         }
         );
    WaitYield();
    Assert.AreEqual(1, badSubErrors.Count, "Expected subscription error - only one subscr allowed");

    // Use of variables in subscription selection subset - not allowed
    badSubErrors.Clear();
    badSubRequest = @"
subscription($prefix: String) {
  sub1: subscribeToThingUpdates(thingId: 1) {
     id name kind 
     idStr(prefix: $prefix)
  }
}";
    var vars = new TVars() { { "prefix", "Id:" } };
    errSubsrc = await client.Subscribe<ThingUpdate>(badSubRequest, null, (c, p) => { },
         (c, err) => {
           badSubErrors.Add(err);
         }
         );
    WaitYield();
    Assert.AreEqual(1, badSubErrors.Count, "Expected subscription error - vars not allowed");
  }// method


  private void WaitYield() {
    for (int i = 0; i < 3; i++) {
      Thread.Sleep(20);
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
