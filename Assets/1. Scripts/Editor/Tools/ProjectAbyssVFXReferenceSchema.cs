#if UNITY_EDITOR
using System;
using System.Collections.Generic;

[Serializable]
internal sealed class VfxAiReferenceReport
{
    public string formatVersion = "3.1";
    public string generatedAtLocal = string.Empty;
    public string unityVersion = string.Empty;
    public string visualEffectGraphPackageVersion = string.Empty;
    public string assetName = string.Empty;
    public string assetPath = string.Empty;
    public string assetGuid = string.Empty;
    public VfxCaptureLayers capture = new();
    public VfxPublicApiRecord publicApi = new();
    public VfxGraphRecord graph = new();
    public List<VfxDependencyRecord> dependencies = new();
    public List<VfxCapabilityRecord> capabilities = new();
    public List<VfxCompiledArtifactRecord> compiledArtifacts = new();
    public VfxPreviewRecord preview = new();
    public VfxExportQuality quality = new();
    public List<string> warnings = new();
    public List<string> errors = new();
}

[Serializable]
internal sealed class VfxCaptureLayers
{
    public bool rawYamlIncluded;
    public bool rawMetaIncluded;
    public bool publicApiCaptured;
    public bool loadedSubAssetsCaptured;
    public bool serializedObjectCaptured;
    public bool yamlFallbackCaptured;
    public bool assetDatabaseDependenciesCaptured;
    public bool yamlGuidDependenciesCaptured;
    public bool renderedPreviewAttempted;
    public bool renderedPreviewSucceeded;
    public int loadedObjectCount;
    public int serializedObjectCount;
    public int yamlDocumentCount;
    public int graphNodeCount;
    public int graphEdgeCount;
    public int dependencyCount;
    public int copiedDependencyCount;
    public int compiledArtifactCount;
    public int coreSerializedPropertyCount;
    public int omittedSerializedPropertyCount;
}

[Serializable]
internal sealed class VfxPublicApiRecord
{
    public List<VfxExposedPropertyRecord> exposedProperties = new();
    public List<string> events = new();
}

[Serializable]
internal sealed class VfxExposedPropertyRecord
{
    public string name = string.Empty;
    public string type = string.Empty;
}

[Serializable]
internal sealed class VfxGraphRecord
{
    public string graphName = string.Empty;
    public int graphVersion;
    public int resourceVersion;
    public string initialEventName = string.Empty;
    public float prewarmDeltaTime;
    public int prewarmStepCount;
    public VfxBoundsRecord suggestedBounds = new();
    public List<VfxGraphNodeRecord> nodes = new();
    public List<VfxGraphEdgeRecord> edges = new();
    public List<VfxSystemRecord> systems = new();
    public List<VfxYamlGroupRecord> uiGroups = new();
    public List<VfxYamlStickyNoteRecord> stickyNotes = new();
    public List<VfxUnresolvedGuidRecord> unresolvedGuids = new();
}

[Serializable]
internal sealed class VfxBoundsRecord
{
    public bool valid;
    public float centerX;
    public float centerY;
    public float centerZ;
    public float sizeX;
    public float sizeY;
    public float sizeZ;
    public string source = string.Empty;
}

[Serializable]
internal sealed class VfxGraphNodeRecord
{
    public long localId;
    public int unityClassId;
    public string unityClassName = string.Empty;
    public string runtimeType = string.Empty;
    public string scriptGuid = string.Empty;
    public string scriptAssetPath = string.Empty;
    public string scriptType = string.Empty;
    public string category = string.Empty;
    public string stage = string.Empty;
    public string displayName = string.Empty;
    public string objectName = string.Empty;
    public string label = string.Empty;
    public string title = string.Empty;
    public long parentLocalId;
    public float positionX;
    public float positionY;
    public bool hasPosition;
    public int yamlStartLine;
    public int yamlEndLine;
    public string yamlObjectFile = string.Empty;
    public string yamlSha256 = string.Empty;
    public List<long> childLocalIds = new();
    public List<VfxSerializedPropertyRecord> properties = new();
    public List<VfxReferenceRecord> references = new();
    public List<VfxKeyValueRecord> importantSettings = new();
    public List<string> detectedCapabilities = new();
}

[Serializable]
internal sealed class VfxSerializedPropertyRecord
{
    public string path = string.Empty;
    public string displayName = string.Empty;
    public string serializedType = string.Empty;
    public string value = string.Empty;
    public int depth;
    public bool isArray;
    public int arraySize = -1;
    public bool valueTruncated;
    public int originalLength;
    public string valueSha256 = string.Empty;
}

[Serializable]
internal sealed class VfxReferenceRecord
{
    public string propertyPath = string.Empty;
    public string directionHint = string.Empty;
    public long targetLocalId;
    public string targetGuid = string.Empty;
    public string targetAssetPath = string.Empty;
    public string targetType = string.Empty;
    public string targetName = string.Empty;
    public bool sameAsset;
    public string sourceLayer = string.Empty;
}

[Serializable]
internal sealed class VfxGraphEdgeRecord
{
    public long sourceLocalId;
    public long targetLocalId;
    public string kind = string.Empty;
    public string propertyPath = string.Empty;
    public string sourceLayer = string.Empty;
}

[Serializable]
internal sealed class VfxSystemRecord
{
    public string name = string.Empty;
    public long dataNodeLocalId;
    public long spawnDataNodeLocalId;
    public string dataType = string.Empty;
    public int capacity;
    public int stripCapacity;
    public int particlePerStripCount;
    public List<long> contextLocalIds = new();
    public List<long> blockLocalIds = new();
    public List<long> operatorLocalIds = new();
    public List<string> stages = new();
    public List<VfxKeyValueRecord> importantSettings = new();
    public List<string> detectedCapabilities = new();
}

[Serializable]
internal sealed class VfxKeyValueRecord
{
    public string key = string.Empty;
    public string value = string.Empty;
    public string source = string.Empty;
}

[Serializable]
internal sealed class VfxYamlGroupRecord
{
    public string title = string.Empty;
    public float x;
    public float y;
    public float width;
    public float height;
    public List<long> modelLocalIds = new();
}

[Serializable]
internal sealed class VfxYamlStickyNoteRecord
{
    public string title = string.Empty;
    public string theme = string.Empty;
    public string textSize = string.Empty;
    public string contents = string.Empty;
    public float x;
    public float y;
    public float width;
    public float height;
}

[Serializable]
internal sealed class VfxDependencyRecord
{
    public string guid = string.Empty;
    public string assetPath = string.Empty;
    public string physicalPath = string.Empty;
    public string mainAssetType = string.Empty;
    public string extension = string.Empty;
    public string sha256 = string.Empty;
    public long byteSize;
    public bool isDirectAssetDatabaseDependency;
    public bool isRecursiveAssetDatabaseDependency;
    public bool discoveredFromYaml;
    public bool discoveredFromSerializedObject;
    public bool isPackageAsset;
    public bool isBuiltInOrUnresolved;
    public bool copied;
    public string copiedRelativePath = string.Empty;
    public string copySkipReason = string.Empty;
    public List<string> roles = new();
    public List<string> usages = new();
}

[Serializable]
internal sealed class VfxCompiledArtifactRecord
{
    public long localId;
    public string name = string.Empty;
    public string runtimeType = string.Empty;
    public string reason = string.Empty;
}

[Serializable]
internal sealed class VfxCapabilityRecord
{
    public string name = string.Empty;
    public string confidence = string.Empty;
    public List<string> evidence = new();
}

[Serializable]
internal sealed class VfxPreviewRecord
{
    public bool attempted;
    public bool rendered;
    public bool usedFallbackAssetIcon;
    public int width;
    public int height;
    public int deterministicSeed;
    public string cameraDescription = string.Empty;
    public string failureReason = string.Empty;
    public List<VfxPreviewFrameRecord> frames = new();
    public string contactSheetFile = string.Empty;
    public string lightBackgroundFrameFile = string.Empty;
    public string fallbackAssetIconFile = string.Empty;
    public int isolatedLayer;
    public int distinctFrameCount;
    public float maxInterFrameDifference;
    public bool allFramesIdentical;
    public string validationReason = string.Empty;
}

[Serializable]
internal sealed class VfxPreviewFrameRecord
{
    public float simulationTime;
    public string file = string.Empty;
    public long aliveParticleCount;
    public float nonBackgroundPixelRatio;
    public bool containsVisibleEffect;
    public bool aliveParticleCountAvailable;
    public string pixelSha256 = string.Empty;
    public float differenceFromPrevious;
}

[Serializable]
internal sealed class VfxExportQuality
{
    public string status = "PARTIAL";
    public float graphObjectCoverage;
    public int yamlDocumentsRepresented;
    public int yamlDocumentsTotal;
    public int unresolvedNodeTypeCount;
    public int unresolvedGuidCount;
    public int externalDependencyCount;
    public int missingDependencyPathCount;
    public bool rawSourceGuaranteesLosslessFallback;
    public List<string> checks = new();
}

[Serializable]
internal sealed class VfxUnresolvedGuidRecord
{
    public string guid = string.Empty;
    public List<string> usages = new();
}

[Serializable]
internal sealed class VfxPackageManifest
{
    public string formatVersion = "1.0";
    public string generatedAtLocal = string.Empty;
    public string sourceAssetPath = string.Empty;
    public string packageStatus = string.Empty;
    public List<VfxPackageFileRecord> files = new();
}

[Serializable]
internal sealed class VfxPackageFileRecord
{
    public string relativePath = string.Empty;
    public long byteSize;
    public string sha256 = string.Empty;
}

[Serializable]
internal sealed class VfxCloneTemplateRecipe
{
    public string schemaVersion = "3.1";
    public string name = string.Empty;
    public string description = string.Empty;
    public string buildMode = "CloneTemplate";
    public string templateAssetPath = string.Empty;
    public string templateGuid = string.Empty;
    public bool preserveTemplateGraph = true;
    public List<string> requiredCapabilities = new();
    public List<VfxCloneOverridePlaceholder> exposedOverrides = new();
}

[Serializable]
internal sealed class VfxCloneOverridePlaceholder
{
    public string propertyName = string.Empty;
    public string propertyType = string.Empty;
    public string value = string.Empty;
}
#endif
