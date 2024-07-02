﻿#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace Kanna.Protecc
{
    public static class KannaProteccMesh
    {
        private static int GetSubmeshIndexForVertex(Mesh mesh, int vertexIndex)
        {
            if (vertexIndex < 0 || vertexIndex >= mesh.vertexCount)
            {
                return -2; // Index is out of bounds
            }

            for (var submeshIndex = 0; submeshIndex < mesh.subMeshCount; submeshIndex++)
            {
                var submeshIndices = mesh.GetIndices(submeshIndex);

                if (Array.IndexOf(submeshIndices, vertexIndex) >= 0)
                {
                    return submeshIndex;
                }
            }

            return -1; // Index not found in any submesh
        }

        public static Mesh EncryptMesh(Renderer renderer, Mesh mesh, float distortRatio, KannaProteccData data, List<Material> IgnoredMaterials)
        {
            if (mesh == null) return null;

            KannaLogger.LogToFile($"Encrypting Mesh: {mesh.name} On Renderer: {renderer.name}", KannaProteccRoot.LogLocation);

            var existingMeshPath = AssetDatabase.GetAssetPath(mesh);

            if (!KannaProteccRoot.IsMeshSupported(mesh))
            {
                KannaLogger.LogToFile($"Asset For Mesh Not Found, Invalid Or Is A Built In Unity Mesh! It Will Be Ignored! -> {mesh.name}: {existingMeshPath ?? ""}", KannaProteccRoot.LogLocation, KannaLogger.LogType.Warning);
                return null;
            }

            KannaLogger.LogToFile($"Existing Mesh Path For {mesh.name} Is {existingMeshPath}", KannaProteccRoot.LogLocation);

            bool Predicate(Material o)
            {
                if (o != null)
                {
                    return true;
                }

                KannaLogger.LogToFile($"Mesh: {mesh.name} - User Error Detected, Extra Material Is Null, Removing Automatically And Carrying On..", KannaProteccRoot.LogLocation, KannaLogger.LogType.Warning);
                return false;
            }

            renderer.sharedMaterials = renderer.sharedMaterials.Where(Predicate).ToArray();

            var newVertices = mesh.vertices;
            var normals = mesh.normals;
            var uv7Offsets = new Vector2[mesh.vertexCount];
            var uv8Offsets = new Vector2[mesh.vertexCount];

            var maxDistance = mesh.bounds.max.magnitude - mesh.bounds.min.magnitude;

            for (var v = 0; v < newVertices.Length; v++)
            {
                var SubIndex = GetSubmeshIndexForVertex(mesh, v);

                switch (SubIndex)
                {
                    case -1:
                        KannaLogger.LogToFile($"Mesh: {mesh.name} - SubMeshIndex Invalid. Vertex Index Was Not Found In Any SubMesh: {v}", KannaProteccRoot.LogLocation, KannaLogger.LogType.Error);
                        continue;
                    case -2:
                        KannaLogger.LogToFile($"Mesh: {mesh.name} - SubMeshIndex Invalid, Vertex Index Was Out Of Bounds: {v}", KannaProteccRoot.LogLocation, KannaLogger.LogType.Error);
                        continue;
                }

                if ((SubIndex + 1) > renderer.sharedMaterials.Length)
                {
                    KannaLogger.LogToFile($"Mesh: {mesh.name} - SubMeshIndex Higher/Invalid Than Amount Of Materials Available! ({(SubIndex + 1)} > {renderer.sharedMaterials.Length}) - Assuming All Past {renderer.sharedMaterials.Length} Is {renderer.sharedMaterials.Length}", KannaProteccRoot.LogLocation, KannaLogger.LogType.Error);
                    SubIndex = renderer.sharedMaterials.Length - 1; // Clamp to last material submesh index
                }
                
                var mat = renderer.sharedMaterials[SubIndex];

                if (mat == null || IgnoredMaterials.Contains(mat)  || !mat.shader.name.Contains("KannaProtecc"))
                {
                    continue;
                }

                Debug.Log($"Resolved Material: {mat.name}: {SubIndex}");

                var minRange = maxDistance * -distortRatio;

                uv7Offsets[v].x = Random.Range(minRange, minRange / 2f);
                uv7Offsets[v].y = Random.Range(minRange, minRange / 2f);

                uv8Offsets[v].x = Random.Range(minRange, minRange / 2f);
                uv8Offsets[v].y = Random.Range(minRange, minRange / 2f);

                var isY = false;

                for (var i = 0; i < (data.ComKey.Length) / 2; i++) // theoretically should be exactly 50% of the length
                {
                    newVertices[v] += normals[v] * ((!isY ? uv7Offsets[v].x : uv7Offsets[v].y) * data.ComKey[i]);
                    isY = !isY;
                }

                isY = !isY;

                for (var i = (data.ComKey.Length) / 2; i < data.ComKey.Length; i++) // theoretically should be exactly after 50%
                {
                    newVertices[v] += normals[v] * ((!isY ? uv8Offsets[v].x : uv8Offsets[v].y) * data.ComKey[i]);
                    isY = !isY;
                }
            }

            KannaLogger.LogToFile($"Creating Encrypted Mesh..", KannaProteccRoot.LogLocation);

            if (string.IsNullOrEmpty(KannaProteccRoot.Instance.path))
                KannaProteccRoot.Instance.path = KannaProteccRoot.Instance.pathPrefix + KannaProteccRoot.Instance.gameObject.name.Trim();

            if (!AssetDatabase.IsValidFolder(KannaProteccRoot.Instance.path))
            {
                Obfuscator.CreateFolder(KannaProteccRoot.Instance.path);
            }

            var encryptedMeshPath = Path.Combine(KannaProteccRoot.Instance.path, $"{GUID.Generate()}.asset");

            KannaLogger.LogToFile($"Encrypted Mesh Path: {encryptedMeshPath}", KannaProteccRoot.LogLocation);

            var newMesh = new Mesh();

            newMesh.vertices = newVertices;
            newMesh.colors = mesh.colors;
            newMesh.normals = mesh.normals;
            newMesh.tangents = mesh.tangents;
            newMesh.bindposes = mesh.bindposes;
            newMesh.boneWeights = mesh.boneWeights;
            newMesh.indexFormat = mesh.indexFormat;
            newMesh.uv = mesh.uv;
            newMesh.uv2 = mesh.uv2;
            newMesh.uv3 = mesh.uv3;
            newMesh.uv4 = mesh.uv4;
            newMesh.uv5 = mesh.uv5;
            newMesh.uv6 = mesh.uv6;
            newMesh.uv7 = uv7Offsets;
            newMesh.uv8 = uv8Offsets;
            newMesh.bounds = mesh.bounds;
            newMesh.name = mesh.name;
            newMesh.colors32 = mesh.colors32;
            newMesh.subMeshCount = mesh.subMeshCount;

            if (newMesh.subMeshCount != mesh.subMeshCount)
            {
                KannaLogger.LogToFile($"OOPSIE WOOPSIE!! Uwu We made a fucky wucky!! A wittle fucko boingo! The code monkeys at our headquarters are working VEWY HAWD to fix this! (SubMesh Count Mismatch On Mesh: {mesh.name})", KannaProteccRoot.LogLocation, KannaLogger.LogType.Error);
            }

            // transfer sub meshes
            for (var meshIndex = 0; meshIndex < newMesh.subMeshCount; meshIndex++)
            {
                try
                {
                    var triangles = mesh.GetTriangles(meshIndex);

                    newMesh.SetTriangles(triangles, meshIndex);
                }
                catch (Exception e)
                {
                    KannaLogger.LogToFile($"Failed To Transfer Triangles For Mesh: {mesh.name}, {mesh.subMeshCount} != {newMesh.subMeshCount} Somehow. Error: {e}", KannaProteccRoot.LogLocation, KannaLogger.LogType.Warning);
                }
            }

            KannaLogger.LogToFile($"Done, Transferring Blend Shapes..", KannaProteccRoot.LogLocation);

            // transfer blend shapes
            for (var shapeIndex = 0; shapeIndex < mesh.blendShapeCount; shapeIndex++)
            {
                for (var frameIndex = 0; frameIndex < mesh.GetBlendShapeFrameCount(shapeIndex); frameIndex++)
                {
                    var deltaVertices = new Vector3[newVertices.Length];
                    var deltaNormals = new Vector3[newVertices.Length];
                    var deltaTangents = new Vector3[newVertices.Length];
                    mesh.GetBlendShapeFrameVertices(shapeIndex, frameIndex, deltaVertices, deltaNormals, deltaTangents);
                    var weight = mesh.GetBlendShapeFrameWeight(shapeIndex, frameIndex);
                    var shapeName = mesh.GetBlendShapeName(shapeIndex);
                    newMesh.AddBlendShapeFrame(shapeName, weight, deltaVertices, deltaNormals, deltaTangents);
                }
            }

            KannaLogger.LogToFile($"Done, Creating Mesh Asset File And Saving Assets..", KannaProteccRoot.LogLocation);

            AssetDatabase.CreateAsset(newMesh, encryptedMeshPath);
            AssetDatabase.SaveAssets();

            return newMesh;
        }
    }
}
#endif
