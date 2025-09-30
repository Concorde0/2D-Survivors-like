using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMG.Survivors;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public struct PlayerTag : IComponentData
{
    
}

public struct CameraTarget : IComponentData
{
    public UnityObjectRef<Transform> CameraTransform;
}

public struct InitializedCameraTargetTag : IComponentData{ }

[MaterialProperty("_AnimationIndex")]
public struct AnimationIndexOverride : IComponentData
{
    public float Value;
}

public enum PlayerAnimationIndex : byte
{
    Movement = 0,
    Idle = 1,
    
    None = byte.MaxValue
}

public struct PlayerAttackData : IComponentData
{
    public Entity AttackPrefab;
    public float CoolDownTime;
    public float3 DetectionSize;
    public CollisionFilter CollisionFilter;
}

public struct PlayerCooldownExpirationTimeStamp : IComponentData, IEnableableComponent
{
    public double Value;
}

public struct GemsCollectedCount : IComponentData
{
    public int Value;
}

public struct UpdateGemUIFlag : IComponentData, IEnableableComponent { }

public class PlayerAuthoring : MonoBehaviour
{
    public GameObject attackPrefab;
    public float coolDownTime;
    public float detectionSize;
    private class Baker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<PlayerTag>(entity);
            AddComponent<InitializedCameraTargetTag>(entity);
            AddComponent<CameraTarget>(entity);
            AddComponent<AnimationIndexOverride>(entity);
            
            var enemyLayer = LayerMask.NameToLayer("Enemy");
            var enemyLayerMask = (uint)math.pow(2, enemyLayer);
            
            var attackCollisionFilter = new CollisionFilter()
            {
                BelongsTo = uint.MaxValue,
                CollidesWith = enemyLayerMask,
            };
            
            AddComponent(entity,new PlayerAttackData()
            {
                AttackPrefab = GetEntity(authoring.attackPrefab, TransformUsageFlags.Dynamic),
                CoolDownTime = authoring.coolDownTime,
                DetectionSize = new float3(authoring.detectionSize),
                CollisionFilter = attackCollisionFilter,
                
            });
            AddComponent<PlayerCooldownExpirationTimeStamp>(entity);
            AddComponent<GemsCollectedCount>(entity);
        }
    }
    
    
}

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct CameraInitializationSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<InitializedCameraTargetTag>();
    }
    
    public void OnUpdate(ref SystemState state)
    {
        if(CameraTargetSingleton.Instance == null) return;
        var cameraTargetTransform = CameraTargetSingleton.Instance.transform;


        var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
        foreach (var (cameraTarget,entity) in SystemAPI.Query<RefRW<CameraTarget>>().WithAll<InitializedCameraTargetTag,PlayerTag>().WithEntityAccess())
        {
            cameraTarget.ValueRW.CameraTransform = cameraTargetTransform;
            ecb.RemoveComponent<InitializedCameraTargetTag>(entity);
        }
        
        ecb.Playback(state.EntityManager); 
    }
}

[UpdateAfter(typeof(TransformSystemGroup))]
public partial struct MoveCameraSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (transform,cameraTarget) in SystemAPI.Query<LocalToWorld, CameraTarget>().WithAll<PlayerTag>().WithNone<InitializedCameraTargetTag>())
        {
            cameraTarget.CameraTransform.Value.position = transform.Position;
        }
    }
}

public partial class PlayerInputSystem : SystemBase
{
    private PlayerInput _input;
        
    protected override void OnCreate()
    {
        _input = new PlayerInput();
        _input.Enable();
    }
    
   
    protected override void OnUpdate()
    {
        var currentInput = (float2)_input.GamePlay.Move.ReadValue<Vector2>();
        foreach (var direction in SystemAPI.Query<RefRW<CharacterMoveDirection>>().WithAll<PlayerTag>())
        {
            direction.ValueRW.Value = currentInput;
        }
            
    }
        
    protected override void OnDestroy()
    {
        _input.Disable();
    }
}

public partial struct PlayerAttackSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PhysicsWorldSingleton>();
        state.RequireForUpdate<BeginInitializationEntityCommandBufferSystem.Singleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var elapsedTime = SystemAPI.Time.ElapsedTime;

        var ecbSystem = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);
        var physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        
        foreach (var (expirationTimeStamp, attackData, transform) in SystemAPI.Query<RefRW<PlayerCooldownExpirationTimeStamp>, PlayerAttackData, LocalTransform>())
        {
            if(expirationTimeStamp.ValueRO.Value > elapsedTime) continue;

            var spawnPosition = transform.Position;
            var minDetectPosition = spawnPosition - attackData.DetectionSize;
            var maxDetectPosition = spawnPosition + attackData.DetectionSize;

            var aabbInput = new OverlapAabbInput
            {
                Aabb = new Aabb()
                {
                    Min = minDetectPosition,
                    Max = maxDetectPosition
                },
                Filter = attackData.CollisionFilter
            };

            var overlapHits = new NativeList<int>(state.WorldUpdateAllocator);
            if (!physicsWorldSingleton.OverlapAabb(aabbInput, ref overlapHits))
            {
                continue;
            }

            var maxDistanceSq = float.MaxValue;
            var closestEnemyPosition = float3.zero;
            foreach (var overlapHit in overlapHits)
            {
                var curEnemyPosition = physicsWorldSingleton.Bodies[overlapHit].WorldFromBody.pos;
                var distanceToPlayerSq = math.distancesq(spawnPosition.xy, curEnemyPosition.xy);
                if (distanceToPlayerSq < maxDistanceSq)
                {
                    maxDistanceSq = distanceToPlayerSq;
                    closestEnemyPosition = curEnemyPosition;
                }
            }
            
            var vectorToClosestEnemy = closestEnemyPosition - spawnPosition;
            var angleToClosestEnemy = math.atan2(vectorToClosestEnemy.y, vectorToClosestEnemy.x);
            var spawnOrientation = quaternion.Euler(0f, 0f, angleToClosestEnemy);
            
            var newAttack = ecb.Instantiate(attackData.AttackPrefab);
            ecb.SetComponent(newAttack, LocalTransform.FromPositionRotation(spawnPosition, spawnOrientation));
            
            expirationTimeStamp.ValueRW.Value = elapsedTime + attackData.CoolDownTime;
            
        }
    }
    
    public partial struct UpdateGemUISystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (gemCount, shouldUpdateUI) in SystemAPI.Query<GemsCollectedCount, EnabledRefRW<UpdateGemUIFlag>>())
            {
                GameUIController.Instance.UpdateGemsCollectedText(gemCount.Value);
                shouldUpdateUI.ValueRW = false;
            }
        }
    }
}
