using UnityEngine;

namespace Reins
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class ClosedReinLoop : MonoBehaviour
    {
        private const int PointCount = 48;

        [SerializeField] private Transform vehicleRoot;
        [SerializeField] private Transform leftHorseHead;
        [SerializeField] private Transform leftGrip;
        [SerializeField] private Transform rightGrip;
        [SerializeField] private Transform rightHorseHead;
        [SerializeField] private LineRenderer rope;
        [SerializeField, Min(1f)] private float slack = 1.06f;
        [SerializeField, Min(1)] private int constraintIterations = 12;
        [SerializeField] private float minimumDeckHeight = 0.80f;
        [SerializeField] private Vector3 localGravity = new Vector3(0f, -9.81f, 0f);
        [SerializeField, Range(0f, 1f)] private float damping = 0.985f;
        [SerializeField, Min(0f)] private float maximumStepSpeed = 6f;
        [SerializeField, Range(0f, 1f)] private float anchorSmoothing = 0.35f;

        private readonly Vector3[] _positions = new Vector3[PointCount];
        private readonly Vector3[] _previous = new Vector3[PointCount];
        private readonly Vector3[] _anchors = new Vector3[RopePhysics.PinCount];
        private readonly Vector3[] _smoothedAnchors = new Vector3[RopePhysics.PinCount];
        private readonly float[] _spanSegmentLengths = new float[RopePhysics.PinCount];
        private bool _anchorsInitialized;

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
                localGravity, Time.deltaTime, minimumDeckHeight, constraintIterations,
                damping, maximumStepSpeed);
            rope.SetPositions(_positions);
            _anchorsInitialized = true;
        }

        private void ReadAnchors()
        {
            ReadAnchor(0, leftHorseHead);
            ReadAnchor(1, leftGrip);
            ReadAnchor(2, rightGrip);
            ReadAnchor(3, rightHorseHead);
        }

        /// <summary>
        /// Reads a pin in carriage space and eases it towards the target. The horse head anchor
        /// inherits the gait animation, so without this the rope snaps to it every frame and looks
        /// detached from the horse.
        /// </summary>
        private void ReadAnchor(int index, Transform source)
        {
            var target = vehicleRoot.InverseTransformPoint(source.position);
            _anchors[index] = _anchorsInitialized
                ? Vector3.Lerp(_smoothedAnchors[index], target, anchorSmoothing)
                : target;
            _smoothedAnchors[index] = _anchors[index];
        }
    }
}
