using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NGraphQL.Model;
using NGraphQL.Client;
using Things;
using Things.GraphQL.Types;
using System.Linq;

namespace NGraphQL.Tests.HttpTests {
  using TDict = Dictionary<string, object>;

  [TestClass]
  public class HttpServerTests {

    [TestInitialize]
    public void Init() {
      TestEnv.Initialize();
    }

    [TestMethod]
    public async Task TestBasicQueries() {
      TestEnv.LogTestMethodStart();
      GraphQLResult result;

      TestEnv.LogTestDescr("bug fix: return array of enum values");
      result = await TestEnv.Client.PostAsync("query { kinds: getAllKinds }");
      result.EnsureNoErrors();
      var allKinds = result.GetTopField<ThingKind[]>("kinds");
      Assert.AreEqual(3, allKinds.Length, "Expected 3 values");

      TestEnv.LogTestDescr("Trying basic query, get all things, with names");
      result = await TestEnv.Client.PostAsync("query { things {name kind theFlags aBCGuids} }");
      result.EnsureNoErrors();
      var things = result.GetTopField<Thing[]>("things");
      var thing0Name = things[0].Name;
      Assert.IsNotNull(result);

      TestEnv.LogTestDescr("invalid query");
      // invalid query - things field needs selection subset
      var errResp = await TestEnv.Client.PostAsync("query { things  }");
      Assert.IsNotNull(errResp);
      Assert.IsTrue(errResp.Errors.Count > 0);
    }

    [TestMethod]
    public async Task TestServerEnumHandling() {
      TestEnv.LogTestMethodStart();

      TestEnv.LogTestDescr("input object with enum fields.");
      var query = @"
query ($objWithEnums: InputObjWithEnums) {
   echo: echoInputObjWithEnums(inpObj: $objWithEnums) 
}";
      var vars = new TDict() {
        { "objWithEnums", new TDict() {
                    { "flags", new string[] { "FLAG_ONE", "FLAG_THREE" } },
                    { "flagsArray", new object[] {
                           new [] {"FLAG_ONE", "FLAG_TWO" },
                           new [] {"FLAG_THREE" }
                      }},
                    { "kind", "KIND_TWO" },
                    }
        },
      };

      var result = await TestEnv.Client.PostAsync(query, vars);
      result.EnsureNoErrors();
      Assert.IsNotNull(result);
      var theFlagsStr = result.GetTopField<string>("echo");
      theFlagsStr = theFlagsStr.Replace(" ", string.Empty);
      Assert.AreEqual("Flags:FlagOne,FlagThree;kind:KindTwo;FlagsArray:[FlagOne,FlagTwo;FlagThree]", theFlagsStr,
        "Invalid inputObjWithEnums echo");
    }


    [TestMethod]
    public async Task TestBugFixes() {
      TestEnv.LogTestMethodStart();
      var server = TestEnv.HttpServerInstance.Server;
      var inpObjTypeDef = (ComplexTypeDef) server.Model.GetTypeDef(typeof(InputObj));
      var todayNullField = inpObjTypeDef.Fields.FirstOrDefault(f => f.Name == "todayNull");
      Assert.IsTrue(todayNullField.Flags.IsSet(FieldFlags.Nullable), "Expected Nullable for todayNull");
      await Task.CompletedTask;
    }

    [TestMethod]
    public async Task TestVariables() {
      string query;
      TDict varsDict;
      GraphQLResult result;

      TestEnv.LogTestMethodStart();

      TestEnv.LogTestDescr("input variables of various types.");
      query = @"
query myQuery($boolVal: Boolean, $longVal: Long, $doubleVal: Double, $strVal: String, $kindVal: ThingKind, $flags: [TheFlags!]) { 
  echo: echoInputValuesWithNulls (boolVal: $boolVal, longVal: $longVal, doubleVal: $doubleVal, strVal: $strVal, 
                                  kindVal: $kindVal, flags: $flags )
}";
      varsDict = new TDict() {
        { "boolVal", true }, { "longVal", 654321 }, { "doubleVal", 543.21 },
        { "kindVal", "KIND_ONE" }, {"flags", new string[] {"FLAG_ONE", "FLAG_TWO"}},
        { "strVal", "SomeString" }
      };
      result = await TestEnv.Client.PostAsync(query, varsDict);
      result.EnsureNoErrors();
      var echoResp = result.GetTopField<string>("echo");
      Assert.AreEqual("True|654321|543.21|SomeString|KindOne|FlagOne, FlagTwo", echoResp); //this is InputObj.ToString()

      TestEnv.LogTestDescr("error - invalid argument values, type mismatch.");
      query = @"
query myQuery($boolVal: Boolean, $longVal: Long, $doubleVal: Double, $strVal: String, $kindVal: ThingKind, $flags: [TheFlags!]) { 
  echo: echoInputValuesWithNulls (boolVal: $longVal, longVal: $doubleVal, doubleVal: $strVal )
}";
      result = await TestEnv.Client.PostAsync(query, varsDict);
      Assert.AreEqual(3, result.Errors.Count, "Expected 3 errors");

      TestEnv.LogTestDescr("complex object type in a variable."); // ----------------------------------------------
      query = @"
query myQuery($inpObj: InputObj!) { 
  result: echoInputObjAsString (inpObj: $inpObj)
}";
      var inpObj = new InputObj() {
        Id = 123, Num = 456, Name = "SomeName",
        Flags = TheFlags.FlagOne | TheFlags.FlagThree, Kind = ThingKind.KindTwo, FlagsArray = new TheFlags[] { TheFlags.FlagOne }
      };
      varsDict = new TDict();
      varsDict["inpObj"] = inpObj;
      result = await TestEnv.Client.PostAsync(query, varsDict);
      result.EnsureNoErrors();
      string strResult = result.GetTopField<string>("result");
      Assert.AreEqual("id:123,name:SomeName,num:456,flags:(FlagOne, FlagThree),kind:KindTwo", strResult);
      //this is InputObj.ToString()


      TestEnv.LogTestDescr("literal object as argument, but with prop values coming from variables."); //------------------
      query = @"
query myQuery($num: Int!, $name: String!) { 
  result: echoInputObjAsString (inpObj: {id: 123, num: $num, name: $name,  flags: [[FLAG_ONE]], kind: KIND_ONE, today: ""2024-01-01"" }) 
}";
      varsDict = new TDict();
      // we cannot use InputObj here, serializer will send first-cap prop names and request will fail
      varsDict["num"] = 456;
      varsDict["name"] = "SomeName";
      result = await TestEnv.Client.PostAsync(query, varsDict);
      result.EnsureNoErrors();
      strResult = result.GetTopField<string>("result");
      Assert.AreEqual("id:123,name:SomeName,num:456,flags:(FlagOne),kind:KindOne", strResult); //this is InputObj.ToString()
    }

    [TestMethod]
    public async Task TestInputObjectAsOutput() {
      string query;
      TDict varsDict;
      GraphQLResult result;

      TestEnv.LogTestMethodStart();

      TestEnv.LogTestDescr("Returning Input object as output (NGraphQL allows this)."); // ----------------------------------------------
      query = @"
query myQuery($inpObj: InputObj!) { 
  retObj: echoInputObj (inpObj: $inpObj) {
             id num name flags kind flagsArray
          }
}";
      var inpObj = new InputObj() {
        Id = 123, Num = 456, Name = "SomeName",
        Flags = TheFlags.FlagOne | TheFlags.FlagThree, Kind = ThingKind.KindTwo, FlagsArray = new TheFlags[] { TheFlags.FlagOne }
      };

      varsDict = new TDict();
      varsDict["inpObj"] = inpObj;
      result = await TestEnv.Client.PostAsync(query, varsDict);
      result.EnsureNoErrors();
      var retObj = result.GetTopField<InputObj>("retObj");
      Assert.AreEqual(inpObj.Id, retObj.Id);
      Assert.AreEqual(inpObj.Name, retObj.Name);
      Assert.AreEqual(inpObj.Num, retObj.Num);
      Assert.AreEqual(inpObj.Flags, retObj.Flags);
      Assert.AreEqual(inpObj.Kind, retObj.Kind);
    }


    [TestMethod]
    public async Task TestGetSchema() {
      TestEnv.LogTestMethodStart();
      var schema = await TestEnv.Client.GetSchemaDocument();
      Assert.IsTrue(!string.IsNullOrWhiteSpace(schema), "expected schema doc");
      TestEnv.LogText("  Success: received Schema doc from server using endpoint '.../schema' ");
      TestEnv.LogText(schema);
    }

    [TestMethod]
    public async Task TestServerValidation() {
      string query;
      TDict vars;
      GraphQLResult result;
      TestEnv.LogTestMethodStart();

      var client = TestEnv.Client;

      TestEnv.LogTestDescr("Validation of input values in resolver code, catching GraphQL exception with errors.");
      query = @"
mutation ($id: Int!, $newName: String!) { 
  th: mutateThingWithValidation(id: $id, newName: $newName) { 
         id, name 
      }
}";
      vars = new TDict() { { "id", -1 }, { "newName", "Name  Tooo  Loooooooooooooooooooooong" } };

      // We do it in ThrowOnError=true, so client throws error automatically
      client.ThrowOnError = true;
      ClientGraphQLException exc = null;
      try {
        result = await client.PostAsync(query, vars);
      } catch(ClientGraphQLException e) {
        exc = e; 
      } finally {
        client.ThrowOnError = false; 
      }
      Assert.AreEqual(2, exc.Errors.Length, "expected errors");
      Assert.AreEqual("Id value may not be negative.", exc.Errors[0].Message);
      Assert.AreEqual("newName too long, max size = 10.", exc.Errors[1].Message);

    }



  }
}
