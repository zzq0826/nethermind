// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Mono.Cecil.Cil;
using System.Reflection;
using AssemblyDefinition = Mono.Cecil.AssemblyDefinition;

namespace Nethermind.Test.Utils;

public static class InsertionFactory
{
    //A cache to store modified assemblies latest paths
    private static Dictionary<string, string> AssemblyNameToPath = new();


    // Clears all known modification references
    public static void Clear()
    {
        AssemblyNameToPath = new();
    }

    // A getter for any type instance from modified Assemblies
    // Note - returns dynamic to get around System.Runtime.Loader.IndividualAssemblyLoadContext errors form (T) casts  
    public static T Get<T>() where T : class
    {
        // Get the assembly path for the specified type
        string pathToT = GetAssemblyPath(typeof(T));
        // Load the assembly from the specified path
        Assembly modifiedAssembly = Assembly.LoadFile(pathToT);
        // Get the type from the loaded assembly
        Type modifiedType = modifiedAssembly.GetType(typeof(T).FullName);
        // Create an instance of the modified type
        var tmp = Activator.CreateInstance(modifiedType);
        // Cast the instance to the desired generic type
        T result = tmp as T;
        // Return the instance
        return result;
    }

    public static void ModifyTypeConstructor<TtoInjectInto>(Type typeToInject, string methodToCallName)
    {
        //Get arg real T type data
        Type typeToInjectInto = typeof(TtoInjectInto);
        ModifyTypeConstructor(typeToInjectInto, typeToInject, methodToCallName);
    }

    // Modify the constructor of a type by injecting a call to a void method, specified by its name,
    // from an external type.
    // Note: A type can be modified multiple times.
    // Note: If the method to call modifies a static variable, avoid placing a breakpoint on the line
    // where the static variable is copied to a temporary variable. Instead, place the breakpoint one
    // line below, as this will allow you to see the expected value as in release mode.
    // Note: Adding a reference to a given type may cause problems with looped references.
    // Example of usage:
    // ```
    // InsertionFactory.ModifyTypeConstructor<TestToBeModified>(typeof(TestInjection), 
    //      nameof(TestInjection.AddCounter)); // Modifies the constructor of TestToBeModified
    // var cntAfter0 = TestInjection.Counter; // Outputs 0
    // var test1 = InsertionFactory.Get<TestToBeModified>(); // Obtains a new instance of TestToBeModified
    // var cntAfter1 = TestInjection.Counter; // Outputs 1
    // ```
    public static void ModifyTypeConstructor(Type typeToInjectInto, Type typeToInject, string methodToCallName)
    {

        // Get the assembly path of the assembly to modify
        var assemblyPath = GetAssemblyPath(typeToInjectInto);

        // Read the assembly
        var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);

        // Add the assembly reference
        assembly.MainModule.ImportReference(typeToInject);

        // Find the type to modify
        var targetType = assembly.MainModule.GetType(typeToInjectInto.FullName);
        if (targetType == null)
        {
            throw new ArgumentException($"Type {typeToInjectInto.FullName} not found in the assembly");
        }

        // Find the constructor of the type
        var targetConstructor = targetType.Methods.FirstOrDefault(x => x.IsConstructor);
        if (targetConstructor == null)
        {
            throw new ArgumentException($"Constructor for type {typeToInjectInto.FullName} not found");
        }

        // Import the method from the external type
        var methodToCall = assembly.MainModule.ImportReference(typeToInject.GetMethod(methodToCallName));

        // Insert the method call at the beginning of the constructor
        var ilProcessor = targetConstructor.Body.GetILProcessor();
        ilProcessor.InsertBefore(targetConstructor.Body.Instructions[0], ilProcessor.Create(OpCodes.Call, methodToCall));

        // Write the modified assembly to a new file
        var modifiedAssemblyPath = Path.Combine(Path.GetTempPath(), $"{typeToInjectInto.Name}_{Guid.NewGuid()}.dll");
        assembly.Write(modifiedAssemblyPath);

        //Add to cached list
        AssemblyNameToPath[assembly.FullName] = modifiedAssemblyPath;
    }

    //Get a patrh to Assembly from cache
    private static string GetAssemblyPath(Type typeToInjectInto)
    {
        string path = "";

        //Get cached or default
        if (!AssemblyNameToPath.TryGetValue(typeToInjectInto.Assembly.FullName, out path))
        {
            path = typeToInjectInto.Assembly.Location;
        }
        return path;
    }
}
