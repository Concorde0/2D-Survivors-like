using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace a_Scripts
{
    public struct EnemySpawnData : IComponentData
    {
        public Entity EnemyPrefab;
        public Entity ReaperPrefab;
        public float SpawnInterval;
        public float SpawnDistance;
    }

    public struct EnemySpawnState : IComponentData
    {
        public float SpawnTimer;
        public float ReaperSpawnTimer;
        public Random Random;
    }
    
    public class EnemySpawnerAuthoring : MonoBehaviour
    {
        public GameObject EnemyPrefab;
        public GameObject ReaperPrefab;
        public float ReaperSpawnTime;
        public float SpawnInterval;
        public float SpawnDistance;
        public uint RandomSeed;
        
        private class Baker : Baker<EnemySpawnerAuthoring>
        {
            public override void Bake(EnemySpawnerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new EnemySpawnData
                {
                    EnemyPrefab = GetEntity(authoring.EnemyPrefab, TransformUsageFlags.Dynamic),
                    ReaperPrefab = GetEntity(authoring.ReaperPrefab, TransformUsageFlags.Dynamic),
                    SpawnInterval = authoring.SpawnInterval,
                    SpawnDistance = authoring.SpawnDistance
                });
                AddComponent(entity, new EnemySpawnState
                {
                    SpawnTimer = 0f,
                    ReaperSpawnTimer = authoring.ReaperSpawnTime,
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
            
            foreach (var (spawnState, spawnData, entity) in SystemAPI.Query<RefRW<EnemySpawnState>, EnemySpawnData>().WithEntityAccess())
            {
                spawnState.ValueRW.ReaperSpawnTimer -= deltaTime;
                if (spawnState.ValueRO.ReaperSpawnTimer <= 0f)
                {
                    var reaper = ecb.Instantiate(spawnData.ReaperPrefab);
                    var reaperSpawnPoint = playerPosition + new float3(15f, 10f, 0f);
                    ecb.SetComponent(reaper, LocalTransform.FromPositionRotationScale(reaperSpawnPoint, quaternion.identity, 4f));

                    var enemyQuery = SystemAPI.QueryBuilder().WithAll<EnemyTag>().Build();
                    var enemies = enemyQuery.ToEntityArray(state.WorldUpdateAllocator);
                    ecb.DestroyEntity(enemies);
                    ecb.DestroyEntity(entity);
                    continue;
                }
                
                spawnState.ValueRW.SpawnTimer -= deltaTime;
                if (spawnState.ValueRO.SpawnTimer > 0f) continue;
                spawnState.ValueRW.SpawnTimer = spawnData.SpawnInterval;

                var newEnemy = ecb.Instantiate(spawnData.EnemyPrefab);
                var spawnAngle = spawnState.ValueRW.Random.NextFloat(0f, math.TAU);
                var spawnPoint = new float3
                {
                    x = math.sin(spawnAngle),
                    y = math.cos(spawnAngle),
                    z = 0f
                };
                spawnPoint *= spawnData.SpawnDistance;
                spawnPoint += playerPosition;

                ecb.SetComponent(newEnemy, LocalTransform.FromPosition(spawnPoint));
            }
        }
    }
}