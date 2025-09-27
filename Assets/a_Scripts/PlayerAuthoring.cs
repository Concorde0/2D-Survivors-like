using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct PlayerTag : IComponentData
{
    
}

public class PlayerAuthoring : MonoBehaviour
{
    private class Baker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<PlayerTag>(entity);
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
