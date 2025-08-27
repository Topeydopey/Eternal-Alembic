// SpawnRouter.cs
using UnityEngine;

public static class SpawnRouter
{
    // Set by the portal before loading a scene; read by SpawnPoint in the new scene
    public static string NextSpawnId;
}
