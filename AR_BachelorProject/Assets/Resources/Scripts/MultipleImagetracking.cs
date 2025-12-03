using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARImagePrefabSpawner : MonoBehaviour
{
    [Serializable]
    public class ImagePrefabMapping
    {
        public string imageName;
        public GameObject prefab;
    }

    [SerializeField] private ARTrackedImageManager trackedImageManager;
    [SerializeField] private ARAnchorManager anchorManager;
    [SerializeField] private List<ImagePrefabMapping> imagePrefabMappings = new();

    private Dictionary<string, GameObject> spawnedPrefabs = new();
    private Dictionary<string, ARAnchor> worldAnchors = new();

    // Total distance in meters across all prefabs
    [SerializeField] private float totalWidth = 1.5f;

    private void Awake()
    {
        if (!trackedImageManager)
            trackedImageManager = GetComponent<ARTrackedImageManager>();

        if (!anchorManager)
            anchorManager = GetComponent<ARAnchorManager>();
    }

    private void OnEnable()
    {
        trackedImageManager.trackablesChanged.AddListener(OnImagesChanged);
    }

    private void OnDisable()
    {
        trackedImageManager.trackablesChanged.RemoveListener(OnImagesChanged);
    }

    private void OnImagesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
    {
        foreach (var trackedImage in args.added)
            CreateIndependentAnchor(trackedImage);
    }

    private async void CreateIndependentAnchor(ARTrackedImage trackedImage)
    {
        string imgName = trackedImage.referenceImage.name;
        if (spawnedPrefabs.ContainsKey(imgName))
            return;

        GameObject prefab = GetPrefab(imgName);
        if (prefab == null)
        {
            Debug.LogWarning($"No prefab mapped for {imgName}");
            return;
        }

        Pose pose = new Pose(trackedImage.transform.position, trackedImage.transform.rotation);

        var result = await anchorManager.TryAddAnchorAsync(pose);
        if (!result.status.IsSuccess())
        {
            Debug.LogError("Anchor creation failed: " + result.status);
            return;
        }

        ARAnchor worldAnchor = result.value;
        worldAnchors[imgName] = worldAnchor;

        GameObject instance = Instantiate(prefab, worldAnchor.transform);
        instance.name = $"{imgName}_Instance";

        // Calculate evenly spaced offsets
        Vector3 prefabOffset = CalculateOffset(imgName);
        instance.transform.localPosition = trackedImage.transform.rotation * prefabOffset;
        instance.transform.localRotation = Quaternion.identity;

        spawnedPrefabs[imgName] = instance;

        Debug.Log($"{imgName} placed with independent world anchor at offset {prefabOffset}.");
    }

    private GameObject GetPrefab(string name)
    {
        foreach (var mapping in imagePrefabMappings)
        {
            if (mapping.imageName == name)
                return mapping.prefab;
        }
        return null;
    }

    private Vector3 CalculateOffset(string name)
    {
        int index = imagePrefabMappings.FindIndex(m => m.imageName == name);
        if (index == -1) return Vector3.zero;

        int count = imagePrefabMappings.Count;
        if (count < 2) return Vector3.zero;

        float spacing = totalWidth / (count - 1);
        float startX = -totalWidth / 2f; // Leftmost position

        return new Vector3(startX + index * spacing + 1, 0f, 0f);
    }
}