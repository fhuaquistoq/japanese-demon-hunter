using UnityEngine;

namespace Reins
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class ClosedReinLoop : MonoBehaviour
    {
        private const int PointCount = 32;

        [SerializeField] private Transform vehicleRoot;
        [SerializeField] private Transform leftHorseHead;
        [SerializeField] private Transform leftGrip;
        [SerializeField] private Transform rightGrip;
        [SerializeField] private Transform rightHorseHead;
        [SerializeField] private LineRenderer rope;
        [SerializeField, Min(1f)] private float slack = 1.18f;
        [SerializeField, Min(1)] private int constraintIterations = 6;
        [SerializeField] private float minimumDeckHeight = 0.73f;
        [SerializeField] private Vector3 localGravity = new Vector3(0f, -2.2f, 0f);

        private readonly Vector3[] _positions = new Vector3[PointCount];
        private readonly Vector3[] _previous = new Vector3[PointCount];
        private readonly Vector3[] _anchors = new Vector3[RopePhysics.PinCount];
        private readonly float[] _spanSegmentLengths = new float[RopePhysics.PinCount];

        private void Awake()
        {
            if (vehicleRoot == null)
            {
                vehicleRoot = transform.parent;
            }
            if (rope == null)
            {
                rope = GetComponent<LineRenderer>();
            }

            rope.useWorldSpace = false;
            rope.loop = true;
            rope.positionCount = PointCount;
            ReadAnchors();
            RopePhysics.InitializeLoop(_positions, _previous, _anchors, slack);
            RopePhysics.CalculateSpanSegmentLengths(
                _anchors, PointCount, slack, _spanSegmentLengths);
            rope.SetPositions(_positions);
        }

        private void LateUpdate()
        {
            if (vehicleRoot == null || leftHorseHead == null || leftGrip == null ||
                rightGrip == null || rightHorseHead == null || rope == null)
            {
                return;
            }

            ReadAnchors();
            RopePhysics.CalculateSpanSegmentLengths(
                _anchors, PointCount, slack, _spanSegmentLengths);
            RopePhysics.SimulateLoop(
                _positions, _previous, _anchors, _spanSegmentLengths,
                localGravity, Time.deltaTime, minimumDeckHeight, constraintIterations);
            rope.SetPositions(_positions);
        }

        private void ReadAnchors()
        {
            _anchors[0] = vehicleRoot.InverseTransformPoint(leftHorseHead.position);
            _anchors[1] = vehicleRoot.InverseTransformPoint(leftGrip.position);
            _anchors[2] = vehicleRoot.InverseTransformPoint(rightGrip.position);
            _anchors[3] = vehicleRoot.InverseTransformPoint(rightHorseHead.position);
        }
    }
}
