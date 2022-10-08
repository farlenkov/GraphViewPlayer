// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    public abstract class BaseEdge : GraphElement
    {
        protected BasePort m_InputPort;
        private Vector2 m_InputPortPosition;
        protected bool m_InputPositionOverridden;
        protected Vector2 m_InputPositionOverride;

        protected BasePort m_OutputPort;
        private Vector2 m_OutputPortPosition;
        protected bool m_OutputPositionOverridden;
        protected Vector2 m_OutputPositionOverride;
        
        internal List<BaseEdge> DraggedEdges { get; }

        public BaseEdge()
        {
            DraggedEdges = new();
            Capabilities
                |= Capabilities.Selectable
                   | Capabilities.Deletable
                   | Capabilities.Movable;
        }

        public bool IsGhostEdge { get; set; }

        public BasePort Output
        {
            get => m_OutputPort;
            set => SetPort(ref m_OutputPort, value);
        }

        public BasePort Input
        {
            get => m_InputPort;
            set => SetPort(ref m_InputPort, value);
        }

        public Vector2 From
        {
            get
            {
                if (m_OutputPositionOverridden) { return m_OutputPositionOverride; }
                if (Output != null && Graph != null) { return m_OutputPortPosition; }
                return Vector2.zero;
            }
        }

        public Vector2 To
        {
            get
            {
                if (m_InputPositionOverridden) { return m_InputPositionOverride; }
                if (Input != null && Graph != null) { return m_InputPortPosition; }
                return Vector2.zero;
            }
        }

        public Orientation InputOrientation => Input?.Orientation ?? (Output?.Orientation ?? Orientation.Horizontal);
        public Orientation OutputOrientation => Output?.Orientation ?? (Input?.Orientation ?? Orientation.Horizontal);

        private void SetPort(ref BasePort portToSet, BasePort newPort)
        {
            if (newPort != portToSet)
            {
                // Clean Up Old Connection
                if (portToSet != null)
                {
                    UntrackPort(portToSet);
                    portToSet.Disconnect(this);
                }

                // Setup New Connection
                portToSet = newPort;
                if (portToSet != null)
                {
                    TrackPort(portToSet);
                    portToSet.Connect(this);
                }

                // Mark Dirty
                OnEdgeChanged();
            }
        }

        public bool IsRealEdge() => Input != null && Output != null;
        public bool IsCandidateEdge() => m_InputPositionOverridden || m_OutputPositionOverridden;

        #region Event Handlers
        protected virtual void OnGeometryChanged(GeometryChangedEvent e)
        {
            UpdateCachedInputPortPosition();
            UpdateCachedOutputPortPosition();
            OnEdgeChanged();
        }

        protected override void OnAddedToGraphView()
        {
            UpdateCachedInputPortPosition();
            UpdateCachedOutputPortPosition();
        }

        protected override void OnRemovedFromGraphView()
        {
            Disconnect();
            ResetLayer();
            UnsetPositionOverrides();
            Selected = false;
        }
        #endregion

        #region Ports
        public void SetPortByDirection(BasePort port)
        {
            if (port.Direction == Direction.Input) { Input = port; }
            else { Output = port; }
        }

        public void Disconnect()
        {
            Input = null;
            Output = null;
        }

        private void TrackPort(BasePort port)
        {
            port.OnPositionChange += OnPortPositionChanged;

            VisualElement current = port.hierarchy.parent;
            while (current != null)
            {
                if (current is GraphView.Layer) { break; }

                // if we encounter our node ignore it but continue in the case there are nodes inside nodes
                if (current != port.ParentNode) { current.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged); }

                current = current.hierarchy.parent;
            }
            if (port.ParentNode != null) { port.ParentNode.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged); }

            OnPortPositionChanged(new() { element = port });
        }

        private void UntrackPort(BasePort port)
        {
            port.OnPositionChange -= OnPortPositionChanged;

            VisualElement current = port.hierarchy.parent;
            while (current != null)
            {
                if (current is GraphView.Layer) { break; }

                // if we encounter our node ignore it but continue in the case there are nodes inside nodes
                if (current != port.ParentNode) { port.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged); }

                current = current.hierarchy.parent;
            }
            if (port.ParentNode != null)
            {
                port.ParentNode.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            }
        }

        private void OnPortPositionChanged(PositionData changeData)
        {
            BasePort p = changeData.element as BasePort;
            if (p != null && Graph != null)
            {
                if (p == m_InputPort) { UpdateCachedInputPortPosition(); }
                else if (p == m_OutputPort) { UpdateCachedOutputPortPosition(); }
                OnEdgeChanged();
            }
        }

        private void UpdateCachedInputPortPosition()
        {
            if (Graph == null || Input == null) { return; }
            m_InputPortPosition = Graph.ContentContainer.WorldToLocal(Input.GetGlobalCenter());
            if (!m_InputPositionOverridden) { m_InputPositionOverride = m_InputPortPosition; }
        }

        private void UpdateCachedOutputPortPosition()
        {
            if (Graph == null || Output == null) { return; }
            m_OutputPortPosition = Graph.ContentContainer.WorldToLocal(Output.GetGlobalCenter());
            if (!m_OutputPositionOverridden) { m_OutputPositionOverride = m_OutputPortPosition; }
        }
        #endregion

        #region Position
        protected abstract void OnEdgeChanged();

        public Vector2 GetInputPositionOverride() => m_InputPositionOverride;
        public Vector2 GetOutputPositionOverride() => m_OutputPositionOverride;
        public bool IsInputPositionOverriden() => m_InputPositionOverridden;
        public bool IsOutputPositionOverriden() => m_OutputPositionOverridden;

        public void SetInputPositionOverride(Vector2 position)
            => SetPositionOverride(
                position, out m_InputPositionOverride, out m_InputPositionOverridden, ref m_InputPort);

        public void SetOutputPositionOverride(Vector2 position)
            => SetPositionOverride(
                position, out m_OutputPositionOverride, out m_OutputPositionOverridden, ref m_OutputPort);

        private void SetPositionOverride(Vector2 position, out Vector2 overrideValueToSet,
            out bool overrideFlagToSet, ref BasePort portToUpdate)
        {
            overrideFlagToSet = true;
            overrideValueToSet = position;
            portToUpdate?.UpdateCapColor();
            OnEdgeChanged();
        }

        public void UnsetPositionOverrides()
        {
            m_InputPositionOverridden = false;
            m_OutputPositionOverridden = false;
            m_InputPort?.UpdateCapColor();
            m_OutputPort?.UpdateCapColor();
            OnEdgeChanged();
        }

        public override Vector2 GetCenter()
        {
            if (m_InputPositionOverridden) { return m_InputPositionOverride; }
            if (m_OutputPositionOverridden) { return m_OutputPositionOverride; }
            return base.GetCenter();
        }
        #endregion

        #region Drag Events
        [EventInterest(typeof(DragBeginEvent), typeof(DragEvent), typeof(DragEndEvent), typeof(DragCancelEvent))]
        protected override void ExecuteDefaultActionAtTarget(EventBase evt)
        {
            base.ExecuteDefaultActionAtTarget(evt);
            if (evt.eventTypeId == DragBeginEvent.TypeId()) OnDragBegin((DragBeginEvent)evt);
            else if (evt.eventTypeId == DragEvent.TypeId()) OnDrag((DragEvent)evt);
            else if (evt.eventTypeId == DragEndEvent.TypeId()) OnDragEnd((DragEndEvent)evt);
            else if (evt.eventTypeId == DragCancelEvent.TypeId()) OnDragCancel((DragCancelEvent)evt);
        }
        
        private void OnDragBegin(DragBeginEvent e)
        {
            // Check if this is a edge drag event 
            if (!IsEdgeDrag(e) || !IsMovable()) return;
            
            // Swallow event
            e.StopImmediatePropagation();
            
            // Accept Drag
            e.AcceptDrag(this);
            
            // Set Drag Threshold
            e.SetDragThreshold(10);
        }

        private void OnDrag(DragEvent e)
        {
            // Swallow event
            e.StopImmediatePropagation();

            // This is the first time we breached the drag threshold
            BasePort anchoredPort;
            bool isDragStart = e.GetUserData() == null;
            if (isDragStart)
            {
                // Record detached port (whichever is closest to the mouse)
                if (Input == null) { anchoredPort = Output; }
                else if (Output == null) { anchoredPort = Input; }
                else
                {
                    Vector2 inputPos = Input.GetGlobalCenter();
                    Vector2 outputPos = Output.GetGlobalCenter();
                    float distanceFromInput = (e.mousePosition - inputPos).sqrMagnitude;
                    float distanceFromOutput = (e.mousePosition - outputPos).sqrMagnitude;
                    anchoredPort = distanceFromInput < distanceFromOutput ? Output : Input;
                }

                // Populate dragged edges
                CollectDraggedEdges(Input == anchoredPort ? Output : Input);

                // Set user data
                e.SetUserData(this);

                // Track for panning
                Graph.TrackElementForPan(this);
            }
            else { anchoredPort = IsInputPositionOverriden() ? Output : Input; }

            Vector2 newPosition = Graph.ContentContainer.WorldToLocal(e.mousePosition);
            for (int i = 0; i < DraggedEdges.Count; i++)
            {
                BaseEdge draggedEdge = DraggedEdges[i];
                if (anchoredPort.Direction == Direction.Input) { draggedEdge.SetOutputPositionOverride(newPosition); }
                else { draggedEdge.SetInputPositionOverride(newPosition); }
            }

            // Only light compatible anchors when dragging an edge, otherwise we can't tell if it's a candidate edge.
            if (isDragStart) { Graph.IlluminateCompatiblePorts(Input == anchoredPort ? Input : Output); }
        }

        private void OnDragEnd(DragEndEvent e)
        {
            // Clear dragged edges
            DraggedEdges.Clear(); // TODO - this is a leak cause it's never called when this element is deleted during a drag

            // Could have been deleted
            if (Graph == null) { return; }

            // Reset ports
            Graph?.IlluminateAllPorts();
        }

        private void OnDragCancel(DragCancelEvent e)
        {
            // Reset position
            bool highlightsReset = false;
            for (int i = 0; i < DraggedEdges.Count; i++)
            {
                BaseEdge edge = DraggedEdges[i];
                if (edge.IsRealEdge())
                {
                    edge.ResetLayer();
                    edge.UnsetPositionOverrides();
                    edge.Selected = false;
                }
                else if (edge.Graph != null)
                {
                    if (!highlightsReset)
                    {
                        edge.Graph.IlluminateAllPorts();
                        highlightsReset = true;
                    }
                    edge.Graph.RemoveElement(edge);
                }
            }
            
            // Clear dragged edges
            DraggedEdges.Clear();

            // Could have been deleted
            if (Graph == null) { return; }

            // Untrack for panning
            Graph.UntrackElementForPan(this, true);

            // Reset ports
            Graph.IlluminateAllPorts();
        }

        private bool IsEdgeDrag<T>(DragAndDropEvent<T> e) where T : DragAndDropEvent<T>, new()
        {
            if ((MouseButton)e.button != MouseButton.LeftMouse) return false;
            if (!e.modifiers.IsNone()) return false;
            return true;
        }
        
        private void CollectDraggedEdges(BasePort draggedPort)
        {
            // Grab dragged edges
            if (draggedPort != null && draggedPort.allowMultiDrag)
            {
                foreach (BaseEdge edge in draggedPort.Connections)
                {
                    if (edge.Selected)
                    {
                        DraggedEdges.Add(edge);
                    }
                }
            }
            else { DraggedEdges.Add(this); }
        }
        #endregion
    }
}