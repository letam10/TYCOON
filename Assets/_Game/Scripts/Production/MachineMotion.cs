using UnityEngine;

namespace TYCOON
{
    public sealed class MachineMotion : MonoBehaviour
    {
        [SerializeField] private ProductionMachine machine;
        [SerializeField] private Transform movingPart;
        [SerializeField] private Vector3 rotationAxis = Vector3.right;
        [SerializeField] private float degreesPerSecond = 120f;
        public void Configure(ProductionMachine source, Transform part) { machine = source; movingPart = part; }
        private void Update()
        {
            if (machine != null && movingPart != null && machine.IsProcessing && !machine.OutputBlocked)
                movingPart.Rotate(rotationAxis, degreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
