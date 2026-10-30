using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityWorkshop.Tests
{
    /// <summary>
    /// Builds stand-in third-party assets outside the kit for adoption tests, records a hash of every vendor
    /// file, and removes the vendor folder and any adopted folders the tests created. No real vendor package is
    /// needed, so these tests cannot cover vendor-specific quirks or real Humanoid models.
    /// </summary>
    sealed class VendorFixture : IDisposable
    {
        public const string Folder = "Assets/ZZ_TestVendor";

        public GameObject Crate { get; private set; }
        public GameObject Robot { get; private set; }
        public Material CrateMaterial { get; private set; }

        readonly HashSet<string> m_AdoptedBefore;

        public VendorFixture()
        {
            m_AdoptedBefore = AdoptedFolders();
            if (AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(Folder));

            // A static prop: off-centre pivot, mesh collider, Standard (built-in) cut-out material.
            CrateMaterial = new Material(Shader.Find("Standard")) { name = "Vendor Standard" };
            CrateMaterial.SetColor("_Color", new Color(0.8f, 0.2f, 0.1f));
            CrateMaterial.SetFloat("_Glossiness", 0.7f);
            CrateMaterial.SetFloat("_Mode", 1f);
            AssetDatabase.CreateAsset(CrateMaterial, $"{Folder}/Vendor Standard.mat");
            var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "Vendor Crate";
            Object.DestroyImmediate(crate.GetComponent<BoxCollider>());
            crate.AddComponent<MeshCollider>();
            crate.transform.localScale = new Vector3(2f, 1.5f, 2f);
            crate.GetComponent<MeshRenderer>().sharedMaterial = CrateMaterial;
            var crateRoot = new GameObject("Vendor Crate");
            crate.transform.SetParent(crateRoot.transform, false);
            crate.transform.localPosition = new Vector3(0.5f, 3f, -0.25f);
            Crate = PrefabUtility.SaveAsPrefabAsset(crateRoot, $"{Folder}/Vendor Crate.prefab");
            Object.DestroyImmediate(crateRoot);

            // A model full of things that fight the workshop rig, with one convertible and one unsupported material.
            var unlit = new Material(Shader.Find("Unlit/Color")) { name = "Vendor Unlit" };
            AssetDatabase.CreateAsset(unlit, $"{Folder}/Vendor Unlit.mat");
            var odd = new Material(Shader.Find("Skybox/Procedural")) { name = "Vendor Skybox" };
            AssetDatabase.CreateAsset(odd, $"{Folder}/Vendor Skybox.mat");
            var robot = new GameObject("Vendor Robot");
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(robot.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            body.GetComponent<MeshRenderer>().sharedMaterial = unlit;
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.transform.SetParent(robot.transform, false);
            head.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            head.GetComponent<MeshRenderer>().sharedMaterial = odd;
            var eyes = new GameObject("Eyes", typeof(Camera), typeof(AudioListener));
            eyes.transform.SetParent(robot.transform, false);
            robot.AddComponent<Rigidbody>();
            robot.AddComponent<CharacterController>();
            robot.AddComponent<Animator>().applyRootMotion = true;
            robot.AddComponent<VendorSpinner>();
            Robot = PrefabUtility.SaveAsPrefabAsset(robot, $"{Folder}/Vendor Robot.prefab");
            Object.DestroyImmediate(robot);
            AssetDatabase.SaveAssets();
        }

        /// <summary>SHA-256 of every vendor file, including .meta files (which hold the GUIDs).</summary>
        public static Dictionary<string, string> Hashes()
        {
            using var sha = SHA256.Create();
            return Directory.GetFiles(Folder, "*", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/'))
                .OrderBy(p => p)
                .ToDictionary(p => p, p => Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p))));
        }

        public static HashSet<string> AdoptedFolders() =>
            AssetDatabase.IsValidFolder(WorkshopPaths.AdoptedAssets)
                ? new HashSet<string>(AssetDatabase.GetSubFolders(WorkshopPaths.AdoptedAssets))
                : new HashSet<string>();

        public void Dispose()
        {
            WorkshopAssetAdoption.FailurePoint = null;
            foreach (var folder in AdoptedFolders().Where(f => !m_AdoptedBefore.Contains(f)))
                AssetDatabase.DeleteAsset(folder);
            AssetDatabase.DeleteAsset(Folder);
        }
    }
}
