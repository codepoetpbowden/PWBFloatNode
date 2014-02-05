using System;
using System.Collections.Generic;
using UnityEngine;

namespace PWBFloatNode
{
    public class PWBFloatNode : PartModule
    {
        private struct TopBottomRadiusAndNode
        {
            float r;
            int nS;

            public TopBottomRadiusAndNode(float _radius, int _nodeSize)
            {
                r = _radius;
                nS = _nodeSize;
            }

            public float radius
            {
                get
                {
                    return r;
                }
            }
            public int nodeSize
            {
                get
                {
                    return nS;
                }
            }

        }

        private struct NodeRing
        {
            public NodeRing(int _count, float _radius)
            {
                nodeCount = _count;
                radius = _radius;
                offsetAngle = 0;
                size = 1;
                nodeRadius = 0.625f;
            }
            public NodeRing(int _count, float _radius, float _offset, float _nodeRadius, int _size)
            {
                nodeCount = _count;
                radius = _radius;
                offsetAngle = (float)((double)_offset / (double)180 * Math.PI);
                size = _size;
                nodeRadius = _nodeRadius;

            }

            public float offsetAngle; // offset angle for this ring
            public int nodeCount; // Number of nodes in this node ring
            public float radius; // distance from the centre of the plate to the centre of the node
            public float nodeRadius; // distance from the centre of the node to the edge of the space reserved for the attached part (ie what size engine are you attaching to this node on the plate?)
            public int size; // attachment node size
        }
        
        private class NodePattern
        {
            public NodePattern()
            {
                rings = new List<NodeRing>();
            }

            public void Add(int _count, float _radius, float _offset,float _nodeRadius,int _size)
            {
                rings.Add(new NodeRing(_count, _radius, _offset, _nodeRadius, _size));
            }

            public List<NodeRing> rings;
        }

        //  A class that represents an AttachNode icon and the time at which it should be hidden. When the node pattern is changed the surface attachemnt nodes will be represented by icons that are displayed momentarily.
        private class TempNodeIcon
        {
            public TempNodeIcon(GameObject go, float _hideAt)
            {
                this.icon = go;
                this.hideAt = _hideAt;
            }

            public GameObject icon;
            public float hideAt;
        }

        [KSPField]
        public string floatNodeKey = "f";
        [KSPField]
        public string nodePatternKey = "p";
        [KSPField]
        public string topNodeSizeKey = "t";


        [KSPField(isPersistant = true)]
        public int nodePattern = -1; // Note that the value -1 is a special case. In this case the maximum number of attachment nodes will be created, but be placed so far away as to be unusable. They need to be present so they can be used by the loading code.

        [KSPField(isPersistant = true)]
        public int topTBNR = 1; 

        private List<NodePattern> nodePatternList; // list of procedurally created AttachNodes

        private List<TempNodeIcon> tempIcons; // a list of the icons that are used to temporarly display the attachment nodes while the node pattern is being changed

        private String nodeIDRoot = "PWBProcNode";
        private int maxProceduralNodes = 20; // THis needs to be the largest number of procedural nodes that are possible.

        private List<TopBottomRadiusAndNode> tbrnList;

        private double plateHeight = 0.1; // This is the distance between the top of the plate (that fits on the bottom of a fuel tank, and the bottom of a plate (that the engines attach to.) 

        private OSD osd;
        /// <summary>
        /// Constructor style setup.
        /// Called in the Part\'s Awake method. 
        /// The model may not be built by this point.
        /// </summary>
        public override void OnAwake()
        {
            // TODO debugging - remove
            // Trace out the attachment nodes at this point
            this.LogAttachmentNodesData("OnAwake");

            // Build the list of possible node patterns
            BuildNodePatternList();
            this.tempIcons = new List<TempNodeIcon>();

            // Build a list of possible top/bottom sizes
            this.tbrnList = new List<TopBottomRadiusAndNode>();
            this.tbrnList.Add(new TopBottomRadiusAndNode (0.25f, 0));
            this.tbrnList.Add(new TopBottomRadiusAndNode(0.5f, 1));
            this.tbrnList.Add(new TopBottomRadiusAndNode(1.0f, 2));
            this.tbrnList.Add(new TopBottomRadiusAndNode(1.5f, 2));
            this.tbrnList.Add(new TopBottomRadiusAndNode(2.0f, 2));
        }

        /// <summary>
        /// Called during the Part startup.
        /// StartState gives flag values of initial state
        /// </summary>
        public override void OnStart(StartState state)
        {
            Debug.Log("PWBFloatNode::OnStart state=" + state.ToString());

            try
            {
                osd = new OSD();

                // Now we need to recreate the pattern of procedural attachement nodes based on the config.
                this.CreateProceduralNodes();



            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// Per-frame update
        /// Called ONLY when Part is ACTIVE!
        /// </summary>
        public override void OnUpdate()
        {
        }

        /// <summary>
        /// Per-physx-frame update
        /// Called ONLY when Part is ACTIVE!
        /// </summary>
        public override void OnFixedUpdate()
        {
        }

      /// <summary>
        /// Called when PartModule is asked to save its values.
        /// Can save additional data here.
        /// </summary>
        /// <param name='node'>The node to save in to</param>
        public override void OnSave(ConfigNode node)
        {

        }

        /// <summary>
        /// Called when PartModule is asked to load its values.
        /// Can load additional data here.
        /// </summary>
        /// <param name='node'>The node to load from</param>
        public override void OnLoad(ConfigNode node)
        {
            try
            {
                // Trace out the config node
                Debug.Log("OnLoad()");
                Debug.Log(TraceConfigNode(node));

                this.BuildMountingPlateMesh();

                // Now we need to recreate the pattern of procedural attachement nodes based on the config.
                this.CreateProceduralNodes();

            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }


        }

        private String TraceConfigNode(ConfigNode cn)
        {
            String output = "Node:" + cn.name + " " + cn.id + "\n{";

            foreach (String name in cn.values.DistinctNames())
            {
                foreach (String value in cn.GetValues(name))
                {
                    output += name + ":" + value +"\n";
                }
            }

            foreach (ConfigNode _cn in cn.nodes)
            {
                output += TraceConfigNode(_cn);
            }

            output += "}\n";

            return output;
        }


        public void OnMouseOver()
        {
            try
            {
                if (HighLogic.LoadedSceneIsEditor)
                {
                    // Refresh the logic that decides whether or not to display the procedural node icons.
                    RefreshNodeIcons();


                    if (Input.GetKey(floatNodeKey))
                    {
                        OnFloatNodeKey();
                    }
                    else if (Input.GetKey(nodePatternKey))
                    {
                        OnNodePatternKey();
                    }
                    else if (Input.GetKey(topNodeSizeKey))
                    {
                        OnTopNodeSizeKey();
                    }

                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void OnNodePatternKey()
        {
            // Debug.Log("OnNodePatternKey{");
            
            // Firstly - do not attempt or allow the node pattern to be changed in any way if there is anything radially attached - only do so if the part is pristine!
            if (HasProceduralAttachments(this.part))
            {
                // Display a message to let the user know that they can't change the node pattern while things are attached.
                osd.Error("Can't change node pattern while parts are connected");
                return;
            }
            
            // Change the nodePattern
            this.nodePattern = (this.nodePattern + 1) % this.nodePatternList.Count;

            // Now rebuild the mesh for the mounting plate. Note that this might takea long time, so we need to consider if we want to do this here, or if we want to do it in some other place that marks confirmation that the attachment node pattern has been accepted (when the mouseover is removed, or similar)
            // Note that it is in doing this that the plateHeight is calculated, which is needed to position the atachment nodes.
            BuildMountingPlateMesh();

            // Add new procedural nodes in a new pattern or move existing ones
            CreateProceduralNodes();
            
            // Create the icons that display the positions of the procedural attachment nodes.
            CreateTempNodeIcons();

         
            // Debug.Log("OnNodePatternKey}");
        }

        private void OnTopNodeSizeKey()
        {
            if (HasProceduralAttachments(this.part))
            {
                // Display a message to let the user know that they can't change the node pattern while things are attached.
                osd.Error("Can't change the size of the mounting plate while parts are connected");
                return;
            }

            // Change the TBNR
            this.topTBNR = (this.topTBNR + 1) % this.tbrnList.Count;

            // Now rebuild the mesh for the mounting plate. Note that this might takea long time, so we need to consider if we want to do this here, or if we want to do it in some other place that marks confirmation that the attachment node pattern has been accepted (when the mouseover is removed, or similar)
            // Note that it is in doing this that the plateHeight is calculated, which is needed to position the atachment nodes.
            BuildMountingPlateMesh();

            // Add new procedural nodes in a new pattern or move existing ones
            CreateProceduralNodes();

            // Create the icons that display the positions of the procedural attachment nodes.
            CreateTempNodeIcons();

        }





        private void CreateTempNodeIcons()
        {
            // Clear out the existing list of attachment icons.
            foreach (TempNodeIcon tni in this.tempIcons)
            {
                GameObject.Destroy(tni.icon);
            }
            this.tempIcons.Clear();

            // Create a new list of attchment icons
            foreach (AttachNode an in this.part.attachNodes)
            {
                if (this.IsNodeProcedural(an) && null == an.attachedPart)
                {
                    AddTempNodeIcon(an);
                }
                else if (an.id == "bottom" && null == an.attachedPart)
                {
                    AddTempNodeIcon(an);
                }
            }
        }

        private void AddTempNodeIcon(AttachNode an)
        {
            GameObject icon = (GameObject)UnityEngine.Object.Instantiate(EditorLogic.fetch.attachNodePrefab);
            icon.gameObject.SetActive(true);
            icon.transform.localScale = Vector3.one * an.radius * ((an.size != 0) ? ((float)an.size) : ((float)an.size + 0.5f));

            icon.transform.position = part.transform.TransformPoint(an.position);
            icon.transform.up = part.transform.TransformDirection(an.orientation);
            icon.renderer.material.color = XKCDColors.Blue;

            this.tempIcons.Add(new TempNodeIcon(icon, Time.time + 0.3f)); // Only display of 0.2 seconds. However this will be update while the mouse is over the part.
        }

        private void RefreshNodeIcons()
        {
            float newHideTime = Time.time + 0.3f;
            foreach (TempNodeIcon tmi in this.tempIcons)
            {
                tmi.hideAt = newHideTime;
            }
        }

        private void TidyTempNodeIcons()
        {
            List<TempNodeIcon> toRemove = new List<TempNodeIcon>();

            float currentTime = Time.time;

            foreach(TempNodeIcon tni in this.tempIcons)
            {
                if (tni.hideAt < currentTime)
                {
                    Debug.Log("Removing icon at " + currentTime + " which was set to be removed at " + tni.hideAt);
                    toRemove.Add(tni);
                }
            }

            // Now remove the ones that we have chosen to remove
            foreach(TempNodeIcon tni in toRemove)
            {
                GameObject.Destroy(tni.icon);
                this.tempIcons.Remove(tni);
            }
        }

        private void CreateProceduralNodes()
        {
            // Debug.Log("CreateProceduralNodes(" + this.nodePattern + "){");

            // TODO remove - debugging
            LogAttachmentNodesData("CreateProceduralNodes1");

            int seqNum = 1;

            // We can not change the attachment nodes in the list as they might be shared with the prefabed part. Instead we will create a new list, and asign it to this instance of the part. 
            List<AttachNode> newList = new List<AttachNode>();

            // First make copies of the top and bottom nodes:
            newList.Add(CopyNode(this.part.attachNodes.Find(an => an.id == "top")));
            newList.Add(CopyNode(this.part.attachNodes.Find(an => an.id == "bottom")));

            // If the node pattern is -1 then we create the maximum number of nodes but place them so far away that they can not be used. This should only happen for the prefab. Once the user start selecting a node pattern, then they should become sensible.
            if (-1 == this.nodePattern)
            {
                for(int i=0;i<this.maxProceduralNodes;i++)
                {
                    newList.Add(this.CreateProceduralNode(-10000, -10000, 1, seqNum));
                    seqNum++;  
                }
            }
            else
            {
                foreach (NodeRing nr in nodePatternList[this.nodePattern].rings)
                {
                    int nodeCount = nr.nodeCount;
                    float radius = nr.radius;

                    // Debug.Log("Adding " + nodeCount + " nodes at a radius of " + radius);

                    // Add the nodes
                    for (int i = 0; i < nodeCount; i++)
                    {
                        float angle = (float)((((float)i / (float)nodeCount) * (Math.PI * 2))) + nr.offsetAngle;

                        float x = (float)Math.Sin(angle) * radius;
                        float z = (float)Math.Cos(angle) * radius;

                        // Debug.Log("nodeCount: " + nodeCount + " i: " + i + " angle: " + angle + " x: " + x + " z: " + z);

                        newList.Add(this.CreateProceduralNode(x, z, nr.size, seqNum));
                        seqNum++;
                    }
                }
            }

            // Now set the list of Attachment Nodes to be this new list.
            this.part.attachNodes = newList;

            // TODO remove - debugging
            LogAttachmentNodesData("CreateProceduralNodes2");
        }

        private AttachNode CreateProceduralNode(float x, float z, int size, int seqNum)
        {
            // Debug.Log("CreateProceduralNode { x:" + x + " z:" + z +" size:"+size + " seqNum:"+seqNum);

            String nodeID = this.nodeIDRoot + seqNum.ToString();
            AttachNode newNode = null; // This will either be a copy of an existing node at that seqNum, or a new creation. TODO would it not be better just to create it afresh each time?

            // Firstly, does a Procedural Node with this SeqNum already exist?
            AttachNode attachNode = this.part.attachNodes.Find(an => an.id == nodeID);

            // If we did not find a ProceduralNode with this SeqNum then create it
            if (null == attachNode)
            {
                // Debug.Log("Adding node: " + nodeID);
                newNode = new AttachNode();
            }
            else
            {
                newNode = this.CopyNode(attachNode);
            }

            newNode.position = new Vector3(x, (float)-this.plateHeight, z);
            newNode.position = newNode.position * (part.rescaleFactor * part.scaleFactor);
            newNode.orientation = new Vector3(0, -1, 0);
            newNode.originalPosition = newNode.position;
            newNode.originalOrientation = newNode.orientation;
            newNode.size = size;
            newNode.attachMethod = AttachNodeMethod.FIXED_JOINT;
            newNode.nodeType = AttachNode.NodeType.Stack;
            newNode.id = nodeID;

            return newNode;
        }

        private bool DeleteProceduralNode(int seqNum)
        {
            Debug.Log("DeleteProceduralNode" + seqNum);

            String nodeID = this.nodeIDRoot + seqNum.ToString();
            
            // Firstly, find a Procedural Node with this SeqNum
            AttachNode attachNode = this.part.attachNodes.Find(an => an.id == nodeID);

            // If we did not find a ProceduralNode with this SeqNum then create it
            if (null == attachNode)
            {
                // The specified procedural node does not exist. Whatever!
                return false;
            }
            else
            {
                this.part.attachNodes.Remove(attachNode);
                return true;
            }
        }

        private void RemoveSurfaceNodes() 
        {
            Debug.Log("RemoveSurfaceNodes{");

            List<AttachNode> listToRemove = new List<AttachNode>();

            // First build a list of nodes to remove, then remove them later. This is because we can not modify a list while it is being enumerated.
            foreach(AttachNode node in this.part.attachNodes)
            {
                if(IsNodeProcedural(node))
                {
                    listToRemove.Add(node);
                }
            }
            foreach (AttachNode node in listToRemove)
            {
                // Destroy the attachment node's "icon" which is the game obhect representing the little ball in the editor.
                if (null != node.icon)
                {
                    UnityEngine.Object.Destroy((UnityEngine.Object)node.icon);
                    node.icon = null;
                }
                this.part.attachNodes.Remove(node);
                Debug.Log("Removed - " + node.id);
            }

            Debug.Log("RemoveSurfaceNodes}");
        }

        // Works out if the are any parts attached to the procedural AttachmentNodes added by this plugin.
        private bool HasProceduralAttachments(Part p)
        {
            // consider all the children of this part
            foreach (Part _childPart in p.children)
            {
                if (IsNodeProcedural(p.findAttachNodeByPart(_childPart)))
                {
                    return true;
                }
            }

            // Also consider the parent
            if (p.parent != null)
            {
                if (IsNodeProcedural(p.findAttachNodeByPart(p.parent)))
                {
                    return true;
                }
            }

            return false;
        }

        // returns true if this is an AttachNode that was created by this mod.
        private bool IsNodeProcedural(AttachNode node)
        {
            if (node.nodeType == AttachNode.NodeType.Stack)
            {
                if (node.id.StartsWith(nodeIDRoot))
                {
                    return true;
                }
            }
            return false;
        }

        private void OnFloatNodeKey()
        {
            if (part.isConnected)
            {
                Debug.Log("Part is connected");
                foreach (AttachNode node in this.part.attachNodes)
                {
                    Debug.Log("considering a node: " + node.id);
                    if (AttachNode.NodeType.Stack == node.nodeType)
                    {
                        Debug.Log("found a stack node");
                        // This is a stack node - it might be top or bottom.
                        // only consider standard anmed top and bottom nodes
                        if (node.id == "bottom" || node.id == "top")
                        {
                            // is this node attached? If not then move the attach node
                            if (null == node.attachedPart)
                            {
                                Vector3 normal = node.orientation;
                                normal.Normalize();
                                float maxd = ProcessParts(part, null, normal);

                                Debug.Log("maxd: " + maxd);

                                // Now that we know how far along the normal the attach node needs to be we can place it
                                if (0 < maxd)
                                {
                                    Debug.Log("node.position: " + node.position);
                                    node.position = normal * maxd * -1.0f; // TODO quick multiply by -1.0 here to make things work, but this needs lots of new thought now that the basepalte is being made procedural
                                    Debug.Log("new node.position: " + node.position);
                                }
                            }
                        }
                    }
                }

                // Since we have potentially moved the float node, display the current node positions
                CreateTempNodeIcons();
                    
            }
        }

        // Calls Process Part on all the children and the parent, if they are surface mounted, but not on the refereing part
        private float ProcessParts(Part _part, Part refferingPart ,Vector3 normal)
        {
            print("Entering ProcessParts");
            float maxd = 0;
            String refferingPartID = null;
            if (refferingPart != null)
            {
                refferingPartID = refferingPart.ConstructID;
            }
            Debug.Log("refferingPart : " + refferingPartID);
            Debug.Log("processing the children of: " + _part.ConstructID);

            foreach (Part _childPart in _part.children)
            {
                if (_childPart.ConstructID != refferingPartID) // ensure that the child is not the reffering part
                {
                    Debug.Log("considering a child part: " + _childPart.ConstructID);
                    AttachNode node = _part.findAttachNodeByPart(_childPart);

                    if (node == null)
                    {
                        Debug.Log("No attach point - the child part must be surface mounted");
                        float d = ProcessPart(_childPart, _part, normal);
                        if (d > maxd) { maxd = d; }
                    }
                    else
                    {
                        if (AttachNode.NodeType.Stack == node.nodeType && refferingPart == null && !IsNodeProcedural(node)) // if the part is stack mounted and the reffering part of null and we did nit create the attachment node then this must be connected to the stack of our own part.
                        {
                            Debug.Log("Not considering this part as it is stack mounted to the orginal part via a node other than one created by this plugin");
                        }
                        else
                        {
                            float d = ProcessPart(_childPart, _part, normal);
                            Debug.Log("d = " + d);
                            if (d > maxd) { maxd = d; }
                        }
                    }
                }
            } // foreach()

            // Also consider the parent
            if (_part.parent != null)
            {
                Debug.Log("considering the parent part: " + _part.parent.ConstructID);
                if (_part.parent.ConstructID != refferingPartID)
                {
                    AttachNode node = _part.findAttachNodeByPart(_part.parent);

                    if (node == null)
                    {
                        Debug.Log("No attach point - the parent part must be surface mounted");
                        float d = ProcessPart(_part.parent, _part, normal);
                        if (d > maxd) { maxd = d; }
                    }
                    else
                    {
                        if (AttachNode.NodeType.Stack == node.nodeType && refferingPart == null && !IsNodeProcedural(node)) // if the part is stack mounted and the reffering part of null then this must be connected to the stack of our wn part.
                        {
                            Debug.Log("Not considering this part as it is stack mounted to the orginal part.");
                        }
                        else
                        {
                            float d = ProcessPart(_part.parent, _part, normal);
                            print("d = " + d);
                            if (d > maxd) { maxd = d; }
                        }
                    }
                }
                else
                {
                    Debug.Log("parent part is the reffering part, so it will not be consdered.");
                }
            }

            Debug.Log("Leaving ProcessParts, maxd:" + maxd);
            
            return (maxd);
        }

        private float ProcessPart(Part _part, Part refferingPart ,Vector3 normal)
        {
            Debug.Log("Entering ProcessPart. part:" + _part.name + " constructID: " + _part.ConstructID);
            float maxd = 0;
            // What is the Normal to the plane? 
//            Vector3 normal = part.transform.rotation * Vector3.up;
            Vector3 pointInPlane = part.transform.localToWorldMatrix.MultiplyPoint3x4(Vector3.zero); // use origin as the point in the plane

            Debug.Log("Normal: " + normal);
            Debug.Log("pointInPlane: " + pointInPlane);
            // go through all the verticies in the collider mesh of the part and find out the one that is furthest away from the plane.

            MeshCollider mc = _part.collider as MeshCollider;
            BoxCollider bc = _part.collider as BoxCollider;

            if (mc)
            {
                Debug.Log("This part has a mesh collider");
                foreach (Vector3 v in mc.sharedMesh.vertices)
                {
                    Vector3 vInWorld = mc.transform.localToWorldMatrix.MultiplyPoint3x4(v);
                    Debug.Log("Considering vertex: " + vInWorld.ToString());
                    float d = GetVertixDistanceFromPlane(vInWorld, normal, pointInPlane);
                    if (d > maxd)
                    {
                        maxd = d;
                    }
                }
            }
            else if (bc)
            {
                // TODO support box colliders (whatever they are!)
                Debug.LogError("Box collider: center: " + bc.center.ToString() + " size: " + bc.size.ToString());
                float d = bc.center.y - bc.size.y;

                if (d > maxd)
                {
                    Debug.Log("d: " +d);
                    maxd = d;
                }
            }
            else
            {
                // TODO
                // Debug.Log("generic collider "+c);
                // addPayload(c.bounds, Matrix4x4.identity);
                Debug.LogError("TODO: generic colliders not yet supported");
            }

            // Also consider all other attached parts
            {
                float d = ProcessParts(_part, refferingPart, normal);
                if(d>maxd) { maxd = d;}
            }

            Debug.Log("Leaving ProcessPart. part: " + _part.name + " maxd: " + maxd);

            return (maxd);
        }

        private float GetVertixDistanceFromPlane(Vector3 point, Vector3 normal, Vector3 pointInPlane)
        {
            float d = Vector3.Dot((pointInPlane - point), normal) / Vector3.Dot(normal, normal);

            Vector3 intersect = (d * normal) + point;

            return (Vector3.Magnitude(point - intersect));
        }


        public void OnGUI()
        {
            EditorLogic editor = EditorLogic.fetch;
            if (editor == null) return;
            if (editor.editorScreen != EditorLogic.EditorScreen.Parts) return;

            osd.Update();

            // TODO consider removing the attachment node icons if we are in the editor and the mouse is not over the part.
            TidyTempNodeIcons();
            
        }

        void BuildNodePatternList()
        {
            this.nodePatternList = new List<NodePattern>();

            // Empty
            {
                NodePattern np = new NodePattern();
                np.Add(0, 0, 0, 0.5f,1);
                this.nodePatternList.Add(np);
            }

            // One in the middle
            {
                NodePattern np = new NodePattern();
                np.Add(1, 0, 0, 0.5f, 1);
                this.nodePatternList.Add(np);
            }

            // Two on either side
            {
                NodePattern np = new NodePattern();
                np.Add(2, 0.5f, 0, 0.5f, 1);
                this.nodePatternList.Add(np);
            }

            // Ring of 3
            {
                NodePattern np = new NodePattern();
                np.Add(3, 0.5f, 0, 0.5f, 1);
                this.nodePatternList.Add(np);
            }

            // Ring of 3 (tight fitting)
            {
                NodePattern np = new NodePattern();
                np.Add(3, 0.577f, 0, 0.5f, 1);
                this.nodePatternList.Add(np);
            }

            // Ring of 4
            {
                NodePattern np = new NodePattern();
                np.Add(4, 0.5f, 0, 0.5f, 1);
                this.nodePatternList.Add(np);
            }

            // Ring of 4 (Tight fitting)
            {
                NodePattern np = new NodePattern();
                np.Add(4, 0.785f, 0, 0.5f, 1);
                this.nodePatternList.Add(np);
            }

            // Ring of 4 offset by 45 degrees
            {
                NodePattern np = new NodePattern();
                np.Add(4, 0.5f, 45, 0.5f, 1);
                this.nodePatternList.Add(np);
            }


            // Ring of 6
            {
                NodePattern np = new NodePattern();
                np.Add(6, 0.5f, 0, 0.5f, 1);
                this.nodePatternList.Add(np);
            }

            // Ring of 6 and one in the middle
            {
                NodePattern np = new NodePattern();
                np.Add(6, 0.5f, 0, 0.5f, 1);
                np.Add(1, 0, 0,0.5f, 1);
                this.nodePatternList.Add(np);
            }

            // Ring of 2 in the edge of a large (2.5m) tank
            {
                NodePattern np = new NodePattern();
                np.Add(2, 1.0f, 0, 0.5f, 1);
                this.nodePatternList.Add(np);
            }

            // Ring of 2 in the edge of a large (2.5m) tank, rotated 90 degrees
            {
                NodePattern np = new NodePattern();
                np.Add(2, 1.0f, 90, 0.5f, 1);
                this.nodePatternList.Add(np);
            }

            // Ring of 4 and one in the middle
            {
                NodePattern np = new NodePattern();
                np.Add(4, 1.0f, 0, 0.5f, 1);
                np.Add(1, 0, 0, 0.5f, 1);
                this.nodePatternList.Add(np);
            }

            // Ring of 6 and one in the middle
            {
                NodePattern np = new NodePattern();
                np.Add(6, 1.0f, 0, 0.5f, 1);
                np.Add(1, 0, 0, 0.5f, 1);
                this.nodePatternList.Add(np);
            }

        }

        private AttachNode CopyNode(AttachNode old)
        {
            AttachNode newNode = new AttachNode();

            newNode.attachedPart = old.attachedPart;
            newNode.attachedPartId = old.attachedPartId;
            newNode.attachMethod = old.attachMethod;
            newNode.breakingForce = old.breakingForce;
            newNode.breakingTorque = old.breakingTorque;
            // TODO deal with the icon newNode.icon.  It is entirely possible that we need not bother as  PartLoader:ParsePart does not set it. However it would be good is we could create csoem attachmentNode icons so the user can see what the new node pattern is when presing 'p'
            newNode.id = old.id;
            // newNode.nodeTransform = new Transform(old.nodeTransform); // I do not think that it is necassery to copy this because it is only used by the new style of attachNode config where you pass in a transform from the unity model. The old stlye config for attachNodes (which we are using for this mode) do not both with the transform. It is only used to xtract the position and orientation anyway! 
            newNode.nodeType = old.nodeType;
            newNode.offset = old.offset;
            newNode.orientation = old.orientation;
            newNode.originalOrientation = old.originalOrientation;
            newNode.originalPosition = old.originalPosition;
            newNode.position = old.position;
            newNode.radius = old.radius;
            newNode.requestGate = old.requestGate;
            newNode.size = old.size;

            return newNode;
        }


        private void LogAttachmentNodesData(String text)
        {
            String logData = "Current attachment nodes for " + this.part.name +" at " + text +":\n";

            foreach(AttachNode an in this.part.attachNodes)
            {
                logData += "Node. id: " + an.id + " x: " + an.position.x + " y: " + an.position.y + " z: " + an.position.z;
            }

            Debug.Log(logData);
        }

        private void BuildMountingPlateMesh()
        {
            // TODO remove debugging
            // Debug.Log("scaleFactor:" + part.scaleFactor + " RescaleFactor:" + part.rescaleFactor);

            // First do soem sanity checking
            if (this.nodePattern < 0) { this.nodePattern = 0; } // This might occur if the user changes the top size before choosing a node pattern.

            double radius = this.tbrnList[this.topTBNR].radius;

            MeshFilter mf = part.FindModelComponent<MeshFilter>("PWBProceduralEngineHousing");
            if (!mf) { Debug.LogError("[PWB FloatNode] no model for the engine mounting1", part); return; }

            // Sort out the rotations and position so that the mesh matches the part.
            mf.transform.position = part.transform.position;
            mf.transform.rotation = part.transform.rotation;

            Mesh m = mf.mesh;
            if (!m) { Debug.LogError("[PWB FloatNode] no model for the engine mounting2", part); return; }

            // Make some decisions - how many faces etc

            int sides = 60; // TODO we need to be MUCH smarter about this!
            double maxCrossSectionRadius = radius;
            double minHeight = 0.1; 
            int totalTriangles = sides * 4;

            double[] shape = new double[sides]; // an array of values representing the radius at the various vertices at the base of the model.
            Vector3[] shapeNormals = new Vector3[sides]; // an array of values representing the normals at the various vertices at the base of the model.


            Vector3[] vertices = new Vector3[(4 * sides) + 2]; // Arrary for the vertices.
            Vector3[] normals = new Vector3[(4 * sides) + 2]; // Arrary for the normal at each vertex
            Color32[] vertexColors = new Color32[(4 * sides) + 2];
            int[] trianges = new int[totalTriangles * 3]; // Array that describes the which vertices make up each triangle.
            Vector2[] uvs = new Vector2[(4 * sides) + 2]; // Marks locations in the uv texture map to particular verticies

            // First locate all the vertices.
            {
                for (int counter = 0; counter < sides; counter++)
                {
                    double crossSectionRadius = radius;
                    double crossSectionAngle = ((double)counter / (double)sides) * (Math.PI * 2);

                    shapeNormals[counter].x = (float)Math.Sin(crossSectionAngle);
                    shapeNormals[counter].y = 0;
                    shapeNormals[counter].z = (float)Math.Cos(crossSectionAngle);

                    foreach (NodeRing ring in this.nodePatternList[this.nodePattern].rings)
                    {
                        for (int counter2 = 0; counter2 < ring.nodeCount; counter2++)
                        {
                            double ringAngle = (Math.PI * 2 * ((double)counter2 / (double)ring.nodeCount)) + (double)ring.offsetAngle;
                            double theta = crossSectionAngle - ringAngle;
                            Debug.Log("theta:" + theta);
                            double result = GetMaxDistanceFromPlateCentre(theta, ring.radius , ring.nodeRadius );
                            if (!double.IsNaN(result))
                            {
                                if (result > crossSectionRadius)
                                {
                                    crossSectionRadius = result;
                                    shapeNormals[counter].x = (float)Math.Sin(ringAngle + theta);
                                    shapeNormals[counter].y = 0; // This needs to be calculated, but we can't do so until we know what the overall height is.
                                    shapeNormals[counter].z = (float)Math.Cos(ringAngle + theta);

                                }
                                crossSectionRadius = Math.Max(crossSectionRadius, result);
                            }
                        }
                    }

                    shape[counter] = crossSectionRadius;
                    maxCrossSectionRadius = Math.Max(maxCrossSectionRadius, crossSectionRadius);

                    // TODO remove debugging
                    Debug.Log("shape[" + counter + "]=" + crossSectionRadius.ToString("F3"));
                }

                plateHeight = minHeight + Math.Max(maxCrossSectionRadius - radius, 0); // Add a multiple in here so we can determine the slope

                // Now that we know the overall height, we can calculate the slopes for the sides, and hence the y componment of the normal vectors
                for (int counter = 0; counter < sides; counter++)
                {
                    shapeNormals[counter].y = (float)(((shape[counter] - radius) * (shape[counter] - radius)) / plateHeight);
                }

                // TODO remove debugging
                Debug.Log("radius:" + radius + "\nmaxCrossSectionRadius:" + maxCrossSectionRadius + "\nheight:" + plateHeight);

                int currentVertex = 0;
                // The central vertex in the center of the plate
                vertices[currentVertex] = Vector3d.zero;
                normals[currentVertex] = Vector3.up;
                //vertexColors[currentVertex] = Color.gray;
                uvs[currentVertex]= new Vector2(0.25f,0.75f);
                currentVertex++;
                
                // the ring of outside vertices at the top.
                // TODO debug remove
                Debug.Log("setting out the top ring of vertices");
                for (int counter1 = 0; counter1 < sides; counter1++)
                {
                    double angle = ((double)counter1 / (double)sides) * (2 * Math.PI);
                    vertices[currentVertex].y = 0;
                    vertices[currentVertex].x = (float)(Math.Sin(angle) * radius);
                    vertices[currentVertex].z = (float)(Math.Cos(angle) * radius);
                    normals[currentVertex] = Vector3.up; // for the top outside ring that is part of the top face, the normal points up.
                    //vertexColors[currentVertex] = Color.blue;
                    uvs[currentVertex] = new Vector2(0.25f + (float)(Math.Sin(angle) * 0.2), 0.75f + (float)(Math.Cos(angle) * 0.2));

                    // Make a copy of the top outside ring. These will be used as the top vertices for the side pieces.They are in the same place by will have different normals so that we can create a clean, square edge around the top.
                    vertices[currentVertex + sides] = vertices[currentVertex];
                    normals[currentVertex + sides] = shapeNormals[counter1]; // for the same top outside vertex that is part of a side face the normal is the one that was precalculated along with the shape of the baseplate.
                    //vertexColors[currentVertex + sides] = Color.white;
                    uvs[currentVertex + sides] = new Vector2(0.05f + ((0.9f * (float)counter1) / (float)sides), 0.45f);
                    currentVertex++;
                }

                // the ring of outside vertices at the bottom.
                // TODO debug remove
                Debug.Log("setting out the bottom ring of vertices");

                // We have already just set two rings of vertices, so we need to skip a ring
                currentVertex = currentVertex + sides;

                for (int counter1 = 0; counter1 < sides; counter1++)
                {
                    double angle = ((double)counter1 / (double)sides) * (2 * Math.PI);
                    vertices[currentVertex].y = (float)-plateHeight;
                    vertices[currentVertex].x = (float)(Math.Sin(angle) * shape[counter1]);
                    vertices[currentVertex].z = (float)(Math.Cos(angle) * shape[counter1]);
                    normals[currentVertex] = shapeNormals[counter1]; // for the top bottom vertex that is part of a side face the normal is the one that was precalculated along with the shape of the baseplate.
                    //vertexColors[currentVertex] = Color.grey;
                    uvs[currentVertex] = new Vector2(0.05f + ((0.9f * (float)counter1) / (float)sides), 0.05f);

                    // Make a copy of the bottom outside ring. These will be used as the outside vertices for the bottom pieces.They are in the same place but will have different normals so that we can create a clean, square edge around the bottom.
                    vertices[currentVertex + sides] = vertices[currentVertex];
                    normals[currentVertex + sides] = Vector3.down; //  the normal for the bottom outside ring that is part ofthe bottom plate is down.
                    //vertexColors[currentVertex + sides] = Color.green;
                    uvs[currentVertex + sides] = new Vector2(0.75f + (float)(Math.Sin(angle) * 0.2),0.75f + (float)(Math.Cos(angle) * 0.2));

                    currentVertex++;
                }


                // Set up the botom centre vertex.
                vertices[currentVertex + sides].x = 0;
                vertices[currentVertex + sides].y = (float)-plateHeight;
                vertices[currentVertex + sides].z = 0;
                normals[currentVertex + sides] = Vector3.down; // the normal for the centre bottom vertex is down.
                //vertexColors[currentVertex + sides] = Color.grey;
                uvs[currentVertex + sides] = new Vector2( 0.75f,0.75f);
            }

            // Now allocate the triangles
            {
                // TODO debug remove
                Debug.Log("setting up the triangles");

                // First the triangles making up the top piece
                for (int counter1 = 0; counter1 < sides; counter1++)
                {
                    trianges[counter1 * 3] = 0;                 // top vertex for a triange on the top
                    trianges[(counter1 * 3) + 1] = counter1 + 1;  // Top outside vertex for a triangle on the top
                    trianges[(counter1 * 3) + 2] = ((counter1 + 1) % sides) + 1;  // another top outside vertext for a triange on the top.

                    trianges[(sides * 3) + (counter1 * 3)] = (sides + 1) + counter1; // top outside vertext for a triangle on the side
                    trianges[(sides * 3) + (counter1 * 3) + 1] = ((2 * sides) + 1) + counter1; // Bottom outside vertext for a triangle on the side
                    trianges[(sides * 3) + (counter1 * 3) + 2] = (sides + 1) + ((counter1 + 1) % sides); // Next top outside vertext for a triangle on the side

                    trianges[(sides * 6) + (counter1 * 3)] = (sides + 1) + ((counter1 + 1) % sides); // Next top outside vertext for a different triangle on the side
                    trianges[(sides * 6) + (counter1 * 3) + 1] = ((2 * sides) + 1) + counter1; // Bottom outside vertext for a differnt triangle on the side
                    trianges[(sides * 6) + (counter1 * 3) + 2] = ((2 * sides) + 1) + ((counter1 + 1) % sides); // Next Bottom outside vertext for a differnt triangle on the side

                    trianges[(sides * 9) + (counter1 * 3)] = ((3 * sides) + 1) + counter1; // Bottom outside vertext for a triangle on the bottom
                    trianges[(sides * 9) + (counter1 * 3) + 1] = ((4 * sides) + 1); // Bottom centre vertext for triangle on the bottom
                    trianges[(sides * 9) + (counter1 * 3) + 2] = ((3 * sides) + 1) + ((counter1 + 1) % sides); // Next bottom outside vertext for triangle on the bottom
                }
            }

            // Now set up all the vertices triangles etc into the mesh
            {
                m.Clear();
                m.vertices = vertices;
                m.triangles = trianges;
                m.uv = uvs; 
                //m.uv1 = null;
                //m.uv2 = null;
                m.colors32 = vertexColors;

                // TODO is it a good idea optimize in the editor - does it cause a performance problem?
                //if (!HighLogic.LoadedSceneIsEditor) m.Optimize();
                m.Optimize();
            }

            // Finally set the collider mesh to use this new mesh
            {
                MeshCollider mc = part.collider as MeshCollider;

                if (null == mc)
                {
                    Debug.LogError("Failed to access the mesh collider");
                }
                else
                {
                    mc.sharedMesh = null;
                    mc.sharedMesh = m;
                }
            }
        }

        // Consider the cross section our the bottom out our mounting plate. We have got a circle the radius main part of the tank, however there may also be other circles arranged in rings, and parts for these may be beyond the radius of the main part. This function calculates the distance from the centre of the part to the edge, be that of the main part of a mounting ring that overlapps the edge. 
        private double GetMaxDistanceFromPlateCentre(double theta, double radius, double size)
        {
            double result = 0;
            // Theta is the angle made from the centre of the main part to the centre of the mounting ring, to the point we are testing for
            // radius is the radius of the main part
            // size is the size of the circles in the mounting ring
            double O = Math.Tan(theta) * radius;
            double d1 = radius / Math.Cos(theta);
            double b = -2*O*Math.Cos(theta+(Math.PI/2));
            double c = (O *O) - (size *size);

            double discriminant = (b *b) - (4 * c); // If this is negative then there are no results.

            if (discriminant < 0)
            {
                // There will be no soultions - set the result to NaN;
                result = double.NaN;
            }
            else
            {
                // Use the quadratic formula to find the two roots, and keep the largest.
                double d2 = Math.Max((-b + (Math.Sqrt(discriminant))) / 2,(-b - (Math.Sqrt(discriminant))) / 2);
                result = d2 + d1;
            }

            return result;
        }
    }

    // Utils - Borrowed from KSP Select Root Mod - credit where it is due
    public class OSD
    {
        private class Message
        {
            public String text;
            public Color color;
            public float hideAt;
        }

        private List<OSD.Message> msgs = new List<OSD.Message>();

        private static GUIStyle CreateStyle(Color color)
        {
            GUIStyle style = new GUIStyle();
            style.stretchWidth = true;
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = 20;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = color;
            return style;
        }

        Predicate<Message> pre = delegate(Message m) { return (Time.time >= m.hideAt); };
        Action<Message> showMesssage = delegate(Message m) { GUILayout.Label(m.text, CreateStyle(m.color)); };

        public void Update()
        {
            if (msgs.Count == 0) return;
            msgs.RemoveAll(pre);
            GUILayout.BeginArea(new Rect(0, Screen.height * 0.1f, Screen.width, Screen.height * 0.8f), CreateStyle(Color.white));
            msgs.ForEach(showMesssage);
            GUILayout.EndArea();
        }

        public void Error(String text)
        {
            AddMessage(text, XKCDColors.LightRed);
        }

        public void Success(String text)
        {
            AddMessage(text, XKCDColors.Cerulean);
        }

        public void Info(String text)
        {
            AddMessage(text, XKCDColors.OffWhite);
        }

        public void AddMessage(String text, Color color, float shownFor)
        {
            OSD.Message msg = new OSD.Message();
            msg.text = text;
            msg.color = color;
            msg.hideAt = Time.time + shownFor;
            msgs.Add(msg);
        }

        public void AddMessage(String text, Color color)
        {
            this.AddMessage(text, color, 3);
        }
    }

}
