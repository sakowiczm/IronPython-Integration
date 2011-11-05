using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using IronPython.Hosting;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using NUnit.Framework;

namespace IronPython.Integration
{
    [TestFixture]
    public class IntegrationTests
    {
        [Test]
        public void SimpleExecutionTest()
        {
            ScriptEngine engine = Python.CreateEngine();

            dynamic result = engine.Execute(@"2+2");

            Assert.IsTrue(result == 4);  
        }

        [Test]
        public void PassingParameterTest()
        {
            ScriptEngine engine = Python.CreateEngine();
            ScriptScope scope = engine.CreateScope();

            // define function

            string printHello = @"
def PrintHello(name):
    msg = 'Hello ' + name
    print msg
    return msg
";

            // compile

            ScriptSource source = engine.CreateScriptSourceFromString(printHello, SourceCodeKind.Statements);
            CompiledCode compiled = source.Compile();
            compiled.Execute(scope);

            // pass parameter & execute

            dynamic fPringHello = scope.GetVariable("PrintHello");

            var result = engine.Operations.Invoke(fPringHello, "Michal");            

            Assert.IsTrue(result == "Hello Michal");
        }

        [Test]
        public void PassingParametersTest()
        {
            ScriptEngine engine = Python.CreateEngine();
            ScriptScope scope = engine.CreateScope();

            // define function

            string add = @"
def Add(a, b):
    return a + b
";

            // compile

            ScriptSource source = engine.CreateScriptSourceFromString(add, SourceCodeKind.Statements);
            CompiledCode compiled = source.Compile();
            compiled.Execute(scope);

            // execute

            dynamic fAdd = scope.GetVariable("Add");

            dynamic result = engine.Operations.Invoke(fAdd, 2, 4);
            Assert.IsTrue(result == 6);

            result = engine.Operations.Invoke(fAdd, "2", "4");
            Assert.IsTrue(result == "24");

            var parameters = new List<object>();
            parameters.Add(2);
            parameters.Add(4);

            result = engine.Operations.Invoke(fAdd, parameters.ToArray());
            Assert.IsTrue(result == 6);
        }

        [Test]
        public void MixingPythonWithCSharpMethodTest()
        {
            ScriptEngine engine = Python.CreateEngine();
            ScriptScope scope = engine.CreateScope();

            // define function

            string add = @"
def Add(a, b):
    return a + b + RULE.GetValue()
";

            // prepare our rules

            dynamic dynamicRules = new ExpandoObject();
            var rules = dynamicRules as IDictionary<string, dynamic>;
            rules.Add("GetValue", (Func<int>)GetValue);

            scope.SetVariable("RULE", dynamicRules);

            // compile

            ScriptSource source = engine.CreateScriptSourceFromString(add, SourceCodeKind.Statements);
            CompiledCode compiled = source.Compile();
            compiled.Execute(scope);

            // execute

            dynamic fAdd = scope.GetVariable("Add");

            dynamic result = engine.Operations.Invoke(fAdd, 2, 4);

            Assert.IsTrue(result == 10);
        }

        [Test]
        public void InstantiatePythonClassTest()
        {
            ScriptEngine engine = Python.CreateEngine();
            ScriptScope scope = engine.CreateScope();

            // compile

            ScriptSource source = engine.CreateScriptSourceFromFile("simple_class.py");
            source.Execute(scope);

            dynamic @class = scope.GetVariable("simple_class");
            dynamic instance = engine.Operations.CreateInstance(@class);
            dynamic result = engine.Operations.InvokeMember(instance, "avg", 1, 2, 3);

            Assert.IsTrue(result == 2);
        }

        [Test]
        public void ImportingNamespacesTest()
        {
            ScriptEngine engine = Python.CreateEngine();
            ScriptScope scope = engine.CreateScope();

            // define function

            string printHello = @"
from System import Decimal

def DivideAndReturnAsStirng(a,b):
    return str(Decimal.Parse(a) / Decimal.Parse(b))
";

            // compile

            ScriptSource source = engine.CreateScriptSourceFromString(printHello, SourceCodeKind.Statements);
            CompiledCode compiled = source.Compile();
            compiled.Execute(scope);

            // pass parameter & execute

            dynamic function = scope.GetVariable("DivideAndReturnAsStirng");

            var result = engine.Operations.Invoke(function, "11", "3");

            Assert.IsTrue(result == "3,6666666666666666666666666667");
        }

        [Test]
        public void ReturningGenericListFromPythonTest()
        {
            ScriptEngine engine = Python.CreateEngine();
            ScriptScope scope = engine.CreateScope();

            string script = @"
from System.Collections.Generic import *

def IntList():
    a = [1,2,3]
    return List[int](a)

def StringList():
    return List[str](['str1', 'str2', 'str3'])

def PythonListToGeneric():
    return [1,2,3]
";

            ScriptSource source = engine.CreateScriptSourceFromString(script, SourceCodeKind.Statements);
            CompiledCode compiled = source.Compile();
            compiled.Execute(scope);

            var f1 = scope.GetVariable("IntList");
            var f2 = scope.GetVariable("StringList");
            var f3 = scope.GetVariable("PythonListToGeneric");

            var a = (List<int>)engine.Operations.Invoke(f1);
            var b = (List<string>)engine.Operations.Invoke(f2);
            var c = ((IList<object>)engine.Operations.Invoke(f3)).ToList();

            Assert.IsTrue(a.Count == 3);
            Assert.IsTrue(b.Count == 3);
            Assert.IsTrue(c.Count == 3);
        }

        [Test]
        public void GenerateDotNetAssemblyFromPythonScriptTest()
        {
            // The clr.CompileModules is purely a load-time optimization - it doesn't make the scripts directly available to a static languge like C#. 
            // You'll need to host the IronPython runtime, and then you can load the DLL into the runtime and use IronPython's hosting interfaces to access it.

            // generate dll

            ScriptEngine engine = Python.CreateEngine();
            ScriptScope scope = engine.CreateScope();

            string script = @"
import clr

clr.CompileModules(""simple_class.dll"", ""simple_class.py"")
";

            ScriptSource source = engine.CreateScriptSourceFromString(script, SourceCodeKind.Statements);
            CompiledCode compiled = source.Compile();
            compiled.Execute(scope);

            engine = null;
            scope = null;

            // import and execute

            engine = Python.CreateEngine();
            engine.Runtime.LoadAssembly(Assembly.LoadFile(Path.GetFullPath("simple_class.dll")));
            scope = engine.ImportModule("simple_class");

            dynamic @class = scope.GetVariable("simple_class");
            dynamic instance = engine.Operations.CreateInstance(@class);
            dynamic result = engine.Operations.InvokeMember(instance, "avg", 1, 2, 3);

            Assert.IsTrue(result == 2);
        }

        public static int GetValue()
        {
            return 4;
        }
    }
}
