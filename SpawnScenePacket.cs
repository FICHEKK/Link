using UnityEngine;

namespace Networking.Transport
{
    /// <summary>
    /// Sent by the server to the client when the client joins and needs to recreate an already existing networked scene.
    /// </summary>
    public class SpawnScenePacket : Packet
    {
        public (int prefabId, int objectId, bool isOwner, Vector3 position)[] ObjectsToSpawn { get; set; }

        protected override void WritePayload()
        {
            Write(ObjectsToSpawn.Length);

            foreach (var objectToSpawn in ObjectsToSpawn)
            {
                Write(objectToSpawn.prefabId);
                Write(objectToSpawn.objectId);
                Write(objectToSpawn.isOwner);
                this.Write(objectToSpawn.position);
            }
        }

        protected override void ReadPayload()
        {
            var objectsToSpawnCount = ReadInt();
            ObjectsToSpawn = new (int prefabId, int objectId, bool isOwner, Vector3 position)[objectsToSpawnCount];

            for (var i = 0; i < objectsToSpawnCount; i++)
            {
                ObjectsToSpawn[i].prefabId = ReadInt();
                ObjectsToSpawn[i].objectId = ReadInt();
                ObjectsToSpawn[i].isOwner = ReadBool();
                ObjectsToSpawn[i].position = this.ReadVector3();
            }
        }
    }
}
