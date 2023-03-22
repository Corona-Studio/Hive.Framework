using Unity.Mathematics;

namespace Hive.Framework.ECS.Entity
{
    public sealed class PositionalEntity : ObjectEntity
    {
        private float4x4 _worldMatrix;
        private float4x4 _localMatrix;

        public float4x4 WorldMatrix
        {
            get => _worldMatrix;
            set
            {
                _worldMatrix = value;
                UpdateLocalMatrix();
                UpdateChildrenWorldMatrix();
            }
        }

        public float4x4 LocalMatrix
        {
            get => _localMatrix;
            set
            {
                _localMatrix = value;
                UpdateWorldMatrix();
            }
        }

        public override IEntity Parent
        {
            get => parent;
            set
            {
                base.Parent = value;
                UpdateLocalMatrix();
            }
        }

        private void UpdateWorldMatrix()
        {
            if (parent is PositionalEntity positionalParent)
            {
                _worldMatrix = positionalParent.WorldMatrix * _localMatrix;
            }
            else
            {
                _worldMatrix = _localMatrix;
            }

            UpdateChildrenWorldMatrix();
        }
        
        private void UpdateChildrenWorldMatrix()
        {
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                if (child is PositionalEntity positionalEntity)
                    positionalEntity.UpdateWorldMatrix();
            }
        }

        private void UpdateLocalMatrix()
        {
            if (parent is PositionalEntity positionalParent)
            {
                _localMatrix = math.inverse(positionalParent._worldMatrix) * _worldMatrix;
            }
            else
            {
                _localMatrix = _worldMatrix;
            }
        }
    }
}