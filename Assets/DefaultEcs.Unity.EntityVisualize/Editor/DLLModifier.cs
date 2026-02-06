using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace DefaultEcs.Unity.EntityVisualize.Editor
{
    public class DLLModifier : AssetPostprocessor
    {
        private const string TargetDllName = "DefaultEcs.dll";
        private const string WorldType = "DefaultEcs.World";
        private const string PublisherType = "DefaultEcs.Internal.Publisher";
        private const string ComponentReadMessageType = "DefaultEcs.Internal.Message.ComponentReadMessage";
        private const string AddedFieldName = "OnPublish";

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (var path in importedAssets)
            {
                if (path.EndsWith(TargetDllName, StringComparison.OrdinalIgnoreCase))
                {
                    EditorApplication.delayCall += () => ProcessDLL(path);
                }
            }
        }

        private static void ProcessDLL(string dllPath)
        {
            if (!File.Exists(dllPath)) return;

            try
            {
                if (CheckIfAlreadyModified(dllPath))
                {
                    Debug.Log($"[DefaultEcs Patcher] Already patched → skip: {dllPath}");
                    return;
                }

                Modify(dllPath);
                AssetDatabase.ImportAsset(dllPath, ImportAssetOptions.ForceUpdate); // 今回はForceUpdateで確実にリロード
                CompilationPipeline.RequestScriptCompilation();

                Debug.Log($"[DefaultEcs Patcher] Successfully patched {TargetDllName}.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DefaultEcs Patcher] Patch failed\n{e}");
            }
        }

        private static bool CheckIfAlreadyModified(string dllPath)
        {
            try
            {
                using var assembly = AssemblyDefinition.ReadAssembly(dllPath);
                var type = assembly.MainModule.GetType(PublisherType);
                if (type == null) return false;

                if ((type.Attributes & TypeAttributes.Public) == 0) return false;

                var field = type.Fields.FirstOrDefault(f => f.Name == AddedFieldName);
                if (field == null) return false;

                var actionTypeRef = assembly.MainModule.ImportReference(typeof(Action<int, object>));
                return field.FieldType.FullName == actionTypeRef.FullName;
            }
            catch
            {
                return false;
            }
        }

        private static void Modify(string dllPath)
        {
            var readerParams = new ReaderParameters { ReadWrite = true };
            using var assembly = AssemblyDefinition.ReadAssembly(dllPath, readerParams);
            var module = assembly.MainModule;

            var publisherType = module.GetType(PublisherType);
            if (publisherType == null) return;

            publisherType.Attributes &= ~TypeAttributes.NotPublic;
            publisherType.Attributes |= TypeAttributes.Public;

            var actionType = module.ImportReference(typeof(Action<int, object>));
            var onPublishField = new FieldDefinition(AddedFieldName, FieldAttributes.Public | FieldAttributes.Static,
                actionType);
            publisherType.Fields.Add(onPublishField);

            var publishMethod = publisherType.Methods.FirstOrDefault(m =>
                m.Name == "Publish" &&
                m.HasGenericParameters &&
                m.Parameters.Count == 2 &&
                m.Parameters[0].ParameterType.MetadataType == MetadataType.Int32);

            if (publishMethod == null) return;

            publishMethod.ImplAttributes &= ~MethodImplAttributes.AggressiveInlining;
            publishMethod.ImplAttributes |= MethodImplAttributes.NoInlining;

            var processor = publishMethod.Body.GetILProcessor();
            var first = publishMethod.Body.Instructions[0];

            var skipLabel = processor.Create(OpCodes.Nop);

            // if (OnPublish == null) goto skip;
            processor.InsertBefore(first, processor.Create(OpCodes.Ldsfld, onPublishField));
            processor.InsertBefore(first, processor.Create(OpCodes.Brfalse_S, skipLabel));

            // OnPublish(worldId, message boxed)
            processor.InsertBefore(first, processor.Create(OpCodes.Ldsfld, onPublishField)); // this
            processor.InsertBefore(first, processor.Create(OpCodes.Ldarg_0)); // arg0: int worldId

            // arg1: ref/in T → 値をロードしてbox
            processor.InsertBefore(first, processor.Create(OpCodes.Ldarg_1)); // &T

            var param1Type = publishMethod.Parameters[1].ParameterType;
            TypeReference elementType = param1Type;

            if (param1Type.IsByReference)
            {
                elementType = param1Type.GetElementType();
            }

            processor.InsertBefore(first, processor.Create(OpCodes.Ldobj, elementType)); // T value = *(&T)

            // ComponentAddedMessage<T> は struct なので box 必須
            processor.InsertBefore(first, processor.Create(OpCodes.Box, elementType));

            var invoke = module.ImportReference(typeof(Action<int, object>).GetMethod("Invoke"));
            processor.InsertBefore(first, processor.Create(OpCodes.Callvirt, invoke));

            // processor.InsertBefore(first, processor.Create(OpCodes.Nop)); // デバッグ用

            processor.InsertBefore(first, skipLabel);

            var worldType = module.GetType(WorldType);
            if (worldType == null) return;
            
            var worldId = worldType.Fields.FirstOrDefault(p => p.Name == "WorldId");
            if (worldId == null) return;

            worldId.Attributes = FieldAttributes.Public;
            
            var componentReadMessageType = module.GetType(ComponentReadMessageType);
            if (componentReadMessageType == null) return;

            componentReadMessageType.Attributes &= ~TypeAttributes.NotPublic;
            componentReadMessageType.Attributes |= TypeAttributes.Public;

            Debug.Log("[Patcher] Patched Publish<T> with ref/in handling");

            assembly.Write();
        }
    }
}