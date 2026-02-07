using System;
using System.IO;
using System.Linq;
using UnityEditor;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEngine;

namespace DefaultEcs.Unity.EntityVisualize.Editor
{
    public sealed class DLLModifier : AssetPostprocessor
    {
        private const string TargetAssemblyName = "DefaultEcs.dll";

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (var path in importedAssets)
            {
                if (path.EndsWith(TargetAssemblyName, StringComparison.OrdinalIgnoreCase))
                {
                    EditorApplication.delayCall += () => ProcessAssembly(path);
                }
            }
        }

        private static void ProcessAssembly(string dllPath)
        {
            var fullPath = Path.GetFullPath(dllPath);
            if (!File.Exists(dllPath)) return;
            if (!InjectMessageBridge(fullPath))
            {
                Debug.Log($"[DefaultEcs Patcher] Already patched → skip: {dllPath}");
                return;
            }

            Debug.Log($"[DefaultEcs Patcher] Successfully patched {dllPath}.");
            AssetDatabase.Refresh();
        }

        private static bool InjectMessageBridge(string assemblyPath)
        {
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location));
            resolver.AddSearchDirectory(Path.GetDirectoryName(assemblyPath));

            var readerParameters = new ReaderParameters { ReadWrite = true, AssemblyResolver = resolver };

            using (var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, readerParameters))
            {
                var module = assembly.MainModule;
                var messageBridge = module.Types.FirstOrDefault(t => t.FullName == "DefaultEcs.MessageBridge");
                if (messageBridge != null) return false;
                // 1. 既存型の解決
                var worldType = module.Types.First(t => t.FullName == "DefaultEcs.World");
                var worldIdField = worldType.Fields.First(f => f.Name == "WorldId");
                var componentReadMessageType =
                    module.Types.First(t => t.FullName == "DefaultEcs.Internal.Message.ComponentReadMessage");
                var observerTypeRef = module.ImportReference(typeof(IObserver<object>));

                // IObserver<object>.OnNext(object) の参照を確実に作成
                var onNextMethodDef = typeof(IObserver<object>).GetMethod("OnNext");
                var onNextMethod = module.ImportReference(onNextMethodDef);

                // 2. MessageBridge クラスの定義
                messageBridge = new TypeDefinition(
                    "DefaultEcs",
                    "MessageBridge",
                    TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed |
                    TypeAttributes.BeforeFieldInit,
                    module.TypeSystem.Object);

                var worldField =
                    new FieldDefinition("_world", FieldAttributes.Private | FieldAttributes.Static, worldType);
                var observerField = new FieldDefinition("_observer", FieldAttributes.Private | FieldAttributes.Static,
                    observerTypeRef);
                messageBridge.Fields.Add(worldField);
                messageBridge.Fields.Add(observerField);

                // SetObserver
                var setObserverMethod = new MethodDefinition("SetObserver",
                    MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
                setObserverMethod.Parameters.Add(new ParameterDefinition("world", ParameterAttributes.None, worldType));
                setObserverMethod.Parameters.Add(new ParameterDefinition("observer", ParameterAttributes.None,
                    observerTypeRef));
                var ilSet = setObserverMethod.Body.GetILProcessor();
                ilSet.Emit(OpCodes.Ldarg_0);
                ilSet.Emit(OpCodes.Stsfld, worldField);
                ilSet.Emit(OpCodes.Ldarg_1);
                ilSet.Emit(OpCodes.Stsfld, observerField);
                ilSet.Emit(OpCodes.Ret);
                messageBridge.Methods.Add(setObserverMethod);

                // 3. Publish<T>(int, T) の作成
                var publishGeneric = new MethodDefinition("Publish",
                    MethodAttributes.Assembly | MethodAttributes.Static,
                    module.TypeSystem.Void);
                var GP = new GenericParameter("T", publishGeneric);
                publishGeneric.GenericParameters.Add(GP);
                publishGeneric.Parameters.Add(new ParameterDefinition("worldId", ParameterAttributes.None,
                    module.TypeSystem.Int32));
                publishGeneric.Parameters.Add(new ParameterDefinition("message", ParameterAttributes.None, GP));

                // ローカル変数の定義 (object msgObj)
                publishGeneric.Body.InitLocals = true;
                var msgObjVar = new VariableDefinition(module.TypeSystem.Object);
                publishGeneric.Body.Variables.Add(msgObjVar);

                var ilPub = publishGeneric.Body.GetILProcessor();
                var endLabel = ilPub.Create(OpCodes.Ret);

                // if (_world == null) return;
                ilPub.Emit(OpCodes.Ldsfld, worldField);
                ilPub.Emit(OpCodes.Brfalse, endLabel);

                // if (_world.WorldId != worldId) return;
                ilPub.Emit(OpCodes.Ldsfld, worldField);
                ilPub.Emit(OpCodes.Ldfld, worldIdField);
                ilPub.Emit(OpCodes.Ldarg_0);
                ilPub.Emit(OpCodes.Bne_Un, endLabel);

                // if (_observer == null) return;
                ilPub.Emit(OpCodes.Ldsfld, observerField);
                ilPub.Emit(OpCodes.Brfalse, endLabel);

                // object msgObj = (object)message;
                ilPub.Emit(OpCodes.Ldarg_1);
                ilPub.Emit(OpCodes.Box, GP);
                ilPub.Emit(OpCodes.Stloc, msgObjVar);

                // if (msgObj is ComponentReadMessage) return;
                ilPub.Emit(OpCodes.Ldloc, msgObjVar);
                ilPub.Emit(OpCodes.Isinst, componentReadMessageType);
                ilPub.Emit(OpCodes.Brtrue, endLabel);

                // _observer.OnNext(msgObj);
                ilPub.Emit(OpCodes.Ldsfld, observerField);
                ilPub.Emit(OpCodes.Ldloc, msgObjVar);
                ilPub.Emit(OpCodes.Callvirt, onNextMethod);

                ilPub.Append(endLabel);
                messageBridge.Methods.Add(publishGeneric);
                module.Types.Add(messageBridge);

                // 4. Publisher.Publish<T> への注入
                var publisherType = module.Types.First(t => t.FullName == "DefaultEcs.Internal.Publisher");
                var targetMethod = publisherType.Methods.First(m => m.Name == "Publish" && m.HasGenericParameters);
                var targetT = targetMethod.GenericParameters[0];

                // MessageBridge.Publish<T> (targetTを使用) を作成
                var genericPublishRef = new GenericInstanceMethod(publishGeneric);
                genericPublishRef.GenericArguments.Add(targetT);

                var ilTarget = targetMethod.Body.GetILProcessor();
                var lastRet = targetMethod.Body.Instructions.Last(i => i.OpCode == OpCodes.Ret);

                ilTarget.InsertBefore(lastRet, ilTarget.Create(OpCodes.Ldarg_0)); // worldId
                ilTarget.InsertBefore(lastRet, ilTarget.Create(OpCodes.Ldarg_1)); // message (T&)
                ilTarget.InsertBefore(lastRet, ilTarget.Create(OpCodes.Ldobj, targetT)); // Dereference
                ilTarget.InsertBefore(lastRet, ilTarget.Create(OpCodes.Call, genericPublishRef));

                assembly.Write();

                return true;
            }
        }
    }
}