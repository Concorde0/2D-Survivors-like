using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace a_Scripts
{
    public struct EnemySpawnerData : IComponentData
    {
        public Entity EnemyPrefab;
        public float SpawnInterval;
        public float SpawnDistance;
    }

    public struct EnemySpawnState : IComponentData
    {
        public float SpawnTimer;
        public Random Random;
    }
    
    public class EnemySpawnerAuthoring : MonoBehaviour
    {
        public GameObject EnemyPrefab;
        public float SpawnInterval;
        public float SpawnDistance;
        public uint RandomSeed;
        
        private class Baker : Baker<EnemySpawnerAuthoring>
        {
            public override void Bake(EnemySpawnerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new EnemySpawnerData()
                {
                    EnemyPrefab = GetEntity(authoring.EnemyPrefab, TransformUsageFlags.Dynamic),
                    SpawnInterval = authoring.SpawnInterval,
                    SpawnDistance = authoring.SpawnDistance
                });
                AddComponent(entity, new EnemySpawnState()
                {
                    SpawnTimer = 0f,
                    Random = Random.CreateFromIndex(authoring.RandomSeed)
                });
            }
        }
    }
    
    public partial struct EnemySpawnSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<BeginPresentationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var ecbSystem = SystemAPI.GetSingleton<BeginPresentationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);
            
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var playerPosition = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;
            
            foreach (var (spawnState,spawnerData) in SystemAPI.Query<RefRW<EnemySpawnState>,EnemySpawnerData>())
            {
                spawnState.ValueRW.SpawnTimer -= deltaTime;
                if (spawnState.ValueRO.SpawnTimer > 0f) { continue; }
                spawnState.ValueRW.SpawnTimer = spawnerData.SpawnInterval;
                
                var newEnemy = ecb.Instantiate(spawnerData.EnemyPrefab);
                var spawnAngle = spawnState.ValueRW.Random.NextFloat(0f, math.TAU);
                var spawnPoint = new float3
                {
                    x = math.sin(spawnAngle),
                    y = math.cos(spawnAngle),
                    z = 0f
                };
                spawnPoint *= spawnerData.SpawnDistance;
                spawnPoint += playerPosition;
                
                ecb.SetComponent(newEnemy,LocalTransform.FromPosition(spawnPoint));
            }
        }
    }
}