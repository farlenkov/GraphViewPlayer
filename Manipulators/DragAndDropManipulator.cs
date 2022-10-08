using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GraphViewPlayer
{
    public class DragAndDropManipulator : Manipulator
    {
        private readonly List<VisualElement> m_PickList;
        
        private int m_DragThreshold;
        private bool m_MouseDown;
        private bool m_BreachedDragThreshold;
        private object m_UserData;
        private Vector2 m_MouseOrigin;
        private Vector2 m_PreviousMousePosition;
        private VisualElement m_Dragged;
        private VisualElement m_PreviousDropTarget;
        
        #region Constructor
        public DragAndDropManipulator() => m_PickList = new();
        #endregion

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<MouseDownEvent>(OnMouseDown);
            target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            target.RegisterCallback<MouseUpEvent>(OnMouseUp);
            target.RegisterCallback<MouseCaptureOutEvent>(OnMouseCaptureOutEvent);
            target.RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
            target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
            target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
            target.UnregisterCallback<MouseCaptureOutEvent>(OnMouseCaptureOutEvent);
            target.UnregisterCallback<KeyDownEvent>(OnKeyDown);
        }

        #region Event Handlers
        private void OnMouseDown(MouseDownEvent e)
        {
            // Don't tolerate clicking other buttons while dragging 
            if (m_MouseDown)
            {
                CancelDrag(e);
                return;
            }
            
            // Create event
            m_MouseDown = true;
            m_MouseOrigin = e.mousePosition;
            using (DragBeginEvent dragBeginEvent = DragBeginEvent.GetPooled(e))
            {
                // Set parent
                dragBeginEvent.ParentManipulator = this;
                
                // Set correct drag delta
                dragBeginEvent.SetMouseDelta(Vector2.zero);
                
                // Send event
                target.SendEvent(dragBeginEvent);
            }
            
            // Record mouse position
            m_PreviousMousePosition = e.mousePosition;
        }
        
        internal void OnDragBeginComplete(DragBeginEvent dragBeginEvent)
        {
            // Check for cancel request or nobody accepting the event
            if (dragBeginEvent.IsCancelled() || m_Dragged == null || m_Dragged.parent == null)
            {
                Reset();
            }
            else
            {
                // Capture any threshold requests
                m_DragThreshold = dragBeginEvent.GetDragThreshold();
                        
                // Capturing ensures we retain the mouse events even if the mouse leaves the target.
                target.CaptureMouse();
            }
        }
        
        private void OnMouseMove(MouseMoveEvent e)
        {
            // If the mouse isn't down, bail
            if (!m_MouseDown) { return; }
            
            // Check if dragged is still in the hierarchy
            if (m_Dragged.parent == null)
            {
                CancelDrag(e);
                return;
            }

            // Check if we're starting a drag
            if (!m_BreachedDragThreshold)
            {
                // Have we breached the drag threshold?
                Vector2 mouseOriginDiff = m_MouseOrigin - e.mousePosition;
                if (Mathf.Abs(mouseOriginDiff.magnitude) < m_DragThreshold)
                {
                    // Not time yet to start dragging
                    return;
                }
            
                // We are starting a drag!
                m_BreachedDragThreshold = true;
            }
            
            // We breached the threshold, time to drag
            using (DragEvent dragEvent = DragEvent.GetPooled(e))
            {
                // Set parent
                dragEvent.ParentManipulator = this;
                
                // Set target
                dragEvent.target = m_Dragged;
                
                // Set correct drag delta
                dragEvent.SetMouseDelta(e.mousePosition - m_PreviousMousePosition);
                
                // Send event
                target.SendEvent(dragEvent);
            }
            
            // Record mouse position
            m_PreviousMousePosition = e.mousePosition;
        }

        internal void OnDragComplete(DragEvent dragEvent)
        {
            // Check for cancel request
            if (dragEvent.IsCancelled() || m_Dragged.parent == null)
            {
                CancelDrag(dragEvent);
                return;
            } 
            
            // Look for droppable
            VisualElement pick = PickFirstExcluding(dragEvent.mousePosition, m_Dragged);
            if (pick == m_PreviousDropTarget) return;
            Debug.Log($"PICK {pick}"); // Pick isn't excluding all selected........
            
            // Check if we need to fire an exit event
            AttemptDropExit(dragEvent); 
            
            // Record new pick
            m_PreviousDropTarget = pick;
            if (pick == null) return;
            
            // Send drop enter event
            using (DropEnterEvent dropEnterEvent = DropEnterEvent.GetPooled(dragEvent))
            {
                // Set parent
                dropEnterEvent.ParentManipulator = this;
                
                // Set target
                dropEnterEvent.target = pick;

                // Drop enter
                target.SendEvent(dropEnterEvent);
            }
        }

        internal void OnDropEnterComplete(DropEnterEvent dropEnterEvent) { }

        private void OnMouseUp(MouseUpEvent e)
        {
            // If the mouse isn't down or we're not dragging
            if (!m_MouseDown) { return; } 
            
            // Make sure the dragged element still exists 
            if (m_Dragged.parent == null)
            {
                CancelDrag(e);
                return;
            } 
            
            // Send drop event
            using (DropEvent dropEvent = DropEvent.GetPooled(e))
            {
                // Set parent
                dropEvent.ParentManipulator = this;
                
                // Check if we skipped the drop
                if (m_PreviousDropTarget == null)
                {
                    OnDropComplete(dropEvent);
                    return;
                }
                
                // Set target 
                dropEvent.target = m_PreviousDropTarget;
                
                // Send drop event
                Debug.Log($"Sending drop to {m_PreviousDropTarget}");
                target.SendEvent(dropEvent);
            }
        }
        
        internal void OnDropComplete(DropEvent dropEvent)
        {
            // Check if cancelled
            if (dropEvent.IsCancelled() || m_Dragged.parent == null)
            {
                CancelDrag(dropEvent);
                return;
            }
            
            // Complete drag
            using (DragEndEvent dragEndEvent = DragEndEvent.GetPooled(dropEvent))
            {
                // Set parent
                dragEndEvent.ParentManipulator = this;
                
                // Set target
                dragEndEvent.target = m_Dragged;
                
                // Set delta to a value that would reset the drag 
                dragEndEvent.DeltaToDragOrigin = m_MouseOrigin - dragEndEvent.mousePosition;
                
                // Send event
                target.SendEvent(dragEndEvent);
            } 
        }

        internal void OnDragEndComplete(DragEndEvent dragEndEvent)
        {
            // Reset everything
            Reset(); 
        }
        
        private void CancelDrag(EventBase e)
        {
            if (m_BreachedDragThreshold)
            {
                using (DragCancelEvent dragCancelEvent = DragCancelEvent.GetPooled())
                {
                    // Set parent
                    dragCancelEvent.ParentManipulator = this;
                    
                    // Set target
                    dragCancelEvent.target = m_Dragged;
                    
                    // Set mouse delta to a value that would reset the drag 
                    dragCancelEvent.DeltaToDragOrigin = m_MouseOrigin - m_PreviousMousePosition;
                    
                    // Send cancel event
                    target.SendEvent(dragCancelEvent);
                    
                    // Check for drag exit
                    AttemptDropExit(dragCancelEvent);
                }
            }
            else Reset();
        }

        internal void OnDragCancelComplete(DragCancelEvent dragCancelEvent)
        {
            Reset();
        }
        
        private void AttemptDropExit<T>(MouseEventBase<T> e) where T : MouseEventBase<T>, new() 
        {
            if (m_PreviousDropTarget != null)
            {
                using (DropExitEvent dropExitEvent = DropExitEvent.GetPooled())
                {
                    // Set parent
                    dropExitEvent.ParentManipulator = this;
                
                    // Set target
                    dropExitEvent.target = m_PreviousDropTarget;
                    
                    // Send event
                    target.SendEvent(dropExitEvent);
                } 
            }
        }
        
        internal void OnDropExitComplete(DropExitEvent dropExitEvent) { }

        private void OnMouseCaptureOutEvent(MouseCaptureOutEvent e)
        {
            if (m_MouseDown) { CancelDrag(e); }
        }

        private void OnKeyDown(KeyDownEvent e)
        {
            // Only listen to ESC while dragging
            if (e.keyCode != KeyCode.Escape || !m_MouseDown) { return; }

            // Notify the draggable 
            CancelDrag(e);
        }

        internal void SetUserData(object userData) => m_UserData = userData;
        internal object GetUserData() => m_UserData;
        internal void SetDraggedElement(VisualElement element) => m_Dragged = element;
        internal VisualElement GetDraggedElement() => m_Dragged;
        #endregion

        #region Helpers
        private VisualElement PickFirstExcluding(Vector2 position, params VisualElement[] exclude)
        {
            target.panel.PickAll(position, m_PickList);
            if (m_PickList.Count == 0) { return null; }
            VisualElement toFind = null;
            for (int i = 0; i < m_PickList.Count; i++)
            {
                VisualElement visualElement = m_PickList[i];
                if (exclude.Length > 0 && Array.IndexOf(exclude, visualElement) != -1) { continue; }
                toFind = visualElement;
                break;
            }
            m_PickList.Clear();
            return toFind;
        } 

        private void Reset()
        {
            m_Dragged = null;
            m_UserData = null;
            m_MouseDown = false;
            m_MouseOrigin = Vector2.zero;
            m_DragThreshold = 0;
            m_PreviousDropTarget = null;
            m_BreachedDragThreshold = false;
            m_PreviousMousePosition = Vector2.zero;
            target.ReleaseMouse();
        }
        #endregion
    }
    
    #region Events
    public abstract class DragAndDropEvent<T> : MouseEventBase<T> where T : DragAndDropEvent<T>, new()
    {
        protected int m_DragThreshold;
        protected bool m_Cancelled;
        protected Vector2 m_DeltaToOrigin;
        internal DragAndDropManipulator ParentManipulator { get; set; }
        internal VisualElement GetDraggedElement() => ParentManipulator.GetDraggedElement(); 
        internal int GetDragThreshold() => m_DragThreshold;
        internal void SetMouseDelta(Vector2 delta) => mouseDelta = delta;
        public bool IsCancelled() => m_Cancelled;
        public void CancelDrag() => m_Cancelled = true;
        public object GetUserData() => ParentManipulator.GetUserData();

        public Vector2 DeltaToDragOrigin
        {
            get => m_DeltaToOrigin;
            internal set => m_DeltaToOrigin = value;
        } 

        protected override void Init()
        {
            base.Init();
            m_DeltaToOrigin = Vector2.zero;
            m_DragThreshold = 0;
            m_Cancelled = false;
        }
    }
    
    public abstract class DragEventBase<T> : DragAndDropEvent<T> where T : DragEventBase<T>, new()
    {
        public void SetUserData(object data) => ParentManipulator.SetUserData(data);
    }

    public class DragBeginEvent : DragEventBase<DragBeginEvent>
    {
        public void SetDragThreshold(int threshold) => m_DragThreshold = threshold;

        public void AcceptDrag(VisualElement draggedElement)
        {
            ParentManipulator.SetDraggedElement(draggedElement);
        }
        
        protected override void PostDispatch(IPanel panel)
        {
            base.PostDispatch(panel);
            ParentManipulator.OnDragBeginComplete(this);
        }
    }

    public class DragEvent : DragEventBase<DragEvent>
    {
        public void ReplaceDrag(VisualElement draggedElement)
        {
            ParentManipulator.SetDraggedElement(draggedElement);
        }
        
        protected override void PostDispatch(IPanel panel)
        {
            base.PostDispatch(panel);
            ParentManipulator.OnDragComplete(this);
        }
    }

    public class DragEndEvent : DragEventBase<DragEndEvent>
    {
        protected override void PostDispatch(IPanel panel)
        {
            base.PostDispatch(panel);
            ParentManipulator.OnDragEndComplete(this);
        }
    }

    public class DragCancelEvent : DragEventBase<DragCancelEvent>
    {
        protected override void PostDispatch(IPanel panel)
        {
            base.PostDispatch(panel);
            ParentManipulator.OnDragCancelComplete(this);
        }
    }

    public class DropEnterEvent : DragAndDropEvent<DropEnterEvent>
    {
        protected override void PostDispatch(IPanel panel)
        {
            base.PostDispatch(panel);
            ParentManipulator.OnDropEnterComplete(this);
        }
    }

    public class DropEvent : DragAndDropEvent<DropEvent>
    {
        protected override void PostDispatch(IPanel panel)
        {
            base.PostDispatch(panel);
            ParentManipulator.OnDropComplete(this);
        }
    }

    public class DropExitEvent : DragAndDropEvent<DropExitEvent>
    {
        protected override void PostDispatch(IPanel panel)
        {
            base.PostDispatch(panel);
            ParentManipulator.OnDropExitComplete(this);
        }
    }
    #endregion
}