using System;
using System.Collections.Generic;
using UnityEngine;

namespace PWBFloatNode
{
    public class PWBFloatNode : PartModule
    {
        private struct NodeRing
        {
            public NodeRing(int _count, float _radius)
            {
                nodeCount = _count;
                radius = _radius;
                offsetAngle = 0;
                size = 1;
            }
            public NodeRing(int _count, float _radius, float _offset, int _size)
            {
                nodeCount = _count;
                radius = _radius;
                offsetAngle = (float)((double)_offset / (double)180 * Math.PI);
                size = _size;
            }

            public float offsetAngle;
            public int nodeCount;
            public float radius;
            public int size;
        }
        
        private class NodePattern
        {
            public NodePattern()
            {
                rings = new List<NodeRing>();
            }

            public void Add(int _count, float _radius, float _offset,int _size)
            {
                rings.Add(new NodeRing(_count, _radius, _offset, _size));
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

        [KSPField(isPersistant = true)]
        public int nodePattern = -1; // Note that the value -1 is a special case. In this case the maximum number of attachment nodes will be created, but be placed so far away as to be unusable. They need to be present so they can be used by the loading code.

        private List<NodePattern> nodePatternList; // list of procedurally created AttachNodes

        private List<TempNodeIcon> tempIcons; // a list of the icons that are used to temporarly display the attachment nodes while the node pattern is being changed

        private String nodeIDRoot = "PWBProcNode";
        private int maxProceduralNodes = 20; // THis needs to be the largest number of procedural nodes that are possible.

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


        private char[] dictionary;

        private void DumpAssetBase()
        {
            String dic;
            dic = "_ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            this.dictionary = dic.ToCharArray();

            Test("AttachNode");
            Test("_AttachNode");
            Test("AttachNodeIcon");
            Test("_AttachNodeIcon");


            // GenerateAndTest("", 0);
        }
        
        private void GenerateAndTest (String stub, int recurseDepth)
        {
            //Debug.Log("Testing" + stub);
            Test(stub);

            int dicSize = this.dictionary.Length;

            for (int i = 0; i < dicSize; i++)
            {
                String testSubject = stub + this.dictionary[i];
                Test(testSubject);
            }

            // Now recurse
            if(recurseDepth <7)
            for (int i = 0; i < dicSize; i++)
            {
                String testSubject = stub + this.dictionary[i];
                GenerateAndTest(testSubject, recurseDepth+1);
            }
        }

        void Test(String test)
        {
            if(AssetBase.GetPrefab(test) != null)
            {
                Debug.Log("Found '" + test +"' in the Assetbase");
            }
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
                    else if (Input.GetKey("a"))
                    {
                        this.DumpAssetBase();
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
            
            // Add new ones in a new pattern or move existing ones
            {
                this.nodePattern = (this.nodePattern + 1) % this.nodePatternList.Count;
                CreateProceduralNodes();
            }

            // Create the icons that display the positions of the procedural attachment nodes.
            CreateTempNodeIcons();
                     
            // Debug.Log("OnNodePatternKey}");
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

            newNode.position = new Vector3(x, 0, z);
            newNode.position *= this.part.scaleFactor;
            newNode.orientation = new Vector3(0, 1, 0);
            newNode.orientation *= this.part.scaleFactor;
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
                                    node.position = normal * maxd;
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
                np.Add(0, 0, 0, 1);
                this.nodePatternList.Add(np);
            }

            // One in the middle
            {
                NodePattern np = new NodePattern();
                np.Add(1, 0, 0, 1);
                this.nodePatternList.Add(np);
            }

            // Two on either side
            {
                NodePattern np = new NodePattern();
                np.Add(2, 0.625f, 0, 1);
                this.nodePatternList.Add(np);
            }

            // Ring of 3
            {
                NodePattern np = new NodePattern();
                np.Add(3, 0.625f, 0, 1);
                this.nodePatternList.Add(np);
            }

            // Ring of 4
            {
                NodePattern np = new NodePattern();
                np.Add(4, 0.625f, 0, 1);
                this.nodePatternList.Add(np);
            }

            // Ring of 4 offset by 45 degrees
            {
                NodePattern np = new NodePattern();
                np.Add(4, 0.625f, 45, 1);
                this.nodePatternList.Add(np);
            }


            // Ring of 6
            {
                NodePattern np = new NodePattern();
                np.Add(6, 0.625f, 0, 1);
                this.nodePatternList.Add(np);
            }

            // Ring of 6 and one in the middle
            {
                NodePattern np = new NodePattern();
                np.Add(6, 0.625f, 0, 1);
                np.Add(1, 0, 0, 1);
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
