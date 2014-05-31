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


        // A class that represents a Bezier curve in 2d space made up of two end points and two control points.
        private class BezierCurve
        {
            Vector2 startPoint;
            Vector2 startControl;
            Vector2 endControl;
            Vector2 endPoint;

            public BezierCurve(Vector2 _startPoint, Vector2 _startControl, Vector2 _endControl,Vector2 _endPoint)
            {
                this.startPoint = _startPoint;
                this.startControl = _startControl;
                this.endControl = _endControl;
                this.endPoint = _endPoint;
            }

            // This is the moethod to call to get a point in the curve that has been constructed. i represents a number from 0.0 to 1.0 that is how far throiugh the curve the point returned will be.
            public Vector2 PointInCurve(float i)
            {
                Vector2 pass1_1 = Vector2Lerf(startPoint, startControl, i);
                Vector2 pass1_2 = Vector2Lerf(startControl, endControl, i);
                Vector2 pass1_3 = Vector2Lerf(endControl, endPoint, i);
                Vector2 pass2_1 = Vector2Lerf(pass1_1, pass1_2, i);
                Vector2 pass2_2 = Vector2Lerf(pass1_2, pass1_3, i);
                Vector2 result = Vector2Lerf(pass2_1, pass2_2, i);

                return result;
            }

            // method to return a vector part way between start and finish
            private Vector2 Vector2Lerf(Vector2 start, Vector2 finish, float i)
            {
                Vector2 result = new Vector2((start.x * i) + (finish.x * (1 - i)), (start.y * i) + (finish.y * (1 - i)));
                return result;
            }
        }




        // A class to hold all the vertices in a mounting plate that can have are variable number of vertical slices.
        private class PlateBuilder
        {
            public Vector3[] vertices;
            public Vector2[] uv;
            private int sides;
            private int levels;

            public PlateBuilder(int _sides, int _levels)
            { 
                this.sides = _sides;
                this.levels = _levels;

                int verticesInTop = 1 + this.sides;
                int verticesInShape = this.levels * (this.sides + 1);
                int verticesInFlange = 2 * (this.sides + 1);
                int verticesInBottom = 1 + this.sides;

                int vertexCount = verticesInTop + verticesInShape + verticesInFlange + verticesInBottom;

                Debug.Log("Allocating space for " + vertexCount + " vertices.");

                vertices = new Vector3[vertexCount];
                uv = new Vector2[vertexCount];
            }

            public int GetLevelEdgeIdx(int s, int l)
            {
                return ((l * (this.sides+1)) + s);
            }

            public Vector3 GetLevelEdge(int s, int l)
            {
                return (this.vertices[GetLevelEdgeIdx(s, l)]);
            }

            public Vector3 SetLevelEdge(int s, int l, Vector3 value,Vector2 uv)
            {
                int index = GetLevelEdgeIdx(s, l);
                this.vertices[index] = value;
                this.uv[index] = uv;
                return (value);
            }

            public int GetTopEdgeIdx(int i)
            {
                return (GetFlangeBottomIdx(i) + (this.sides+1));
            }

            public Vector3 GetTopEdge(int i)
            {
                return (this.vertices[GetTopEdgeIdx(i)]);
            }

            public Vector3 SetTopEdge(int i, Vector3 value,Vector2 uv)
            {
                int index = GetTopEdgeIdx(i);
                this.vertices[index] = value;
                this.uv[index] = uv;
                return value;
            }

            public int GetBottomEdgeIdx(int i)
            {
                return (GetTopEdgeIdx(i) + (this.sides+1));
            }

            public Vector3 GetBottomEdge(int i)
            {
                return (this.vertices[GetBottomEdgeIdx(i)]);
            }

            public Vector3 SetBottomEdge(int i, Vector3 value,Vector2 uv)
            {
                int index =  GetBottomEdgeIdx(i);
                this.vertices[index] = value;
                this.uv[index] = uv;
                return value;
            }

            public int GetTopIdx()
            {
                return(GetFlangeBottomIdx(this.sides) +1);
            }

            public Vector3 GetTop()
            {
                return (this.vertices[GetTopIdx()]);
            }

            public Vector3 SetTop(Vector3 value,Vector2 uv)
            {
                int index = GetTopIdx();
                this.vertices[index] = value;
                this.uv[index] = uv;
                return value;
            }

            public int GetBottomIdx()
            {
                return (GetTopIdx() + 1);
            }
            
            public Vector3 GetBottom()
            {
                return (this.vertices[GetBottomIdx()]);
            }

            public Vector3 SetBottom(Vector3 value,Vector2 uv)
            {
                int index = GetBottomIdx();
                this.vertices[index] = value;
                this.uv[index] = uv;
                return value;
            }

            public int GetFlangeTopIdx(int i)
            {
                return (GetLevelEdgeIdx(i, this.levels));
            }

            public Vector3 GetFlangeTop(int i)
            {
                return (this.vertices[GetFlangeTopIdx(i)]);
            }

            public Vector3 SetFlangeTop(int i,Vector3 value, Vector2 uv)
            {
                int index = GetFlangeTopIdx(i);
                this.vertices[index] = value;
                this.uv[index] = uv;
                return value;
            }


            public int GetFlangeBottomIdx(int i)
            {
                return (GetFlangeTopIdx(i) + (this.sides+1));
            }

            public Vector3 GetFlangeBottom(int i)
            {
                return (this.vertices[GetFlangeBottomIdx(i)]);
            }

            public Vector3 SetFlangeBottom(int i, Vector3 value, Vector2 uv)
            {
                int index = GetFlangeBottomIdx(i);
                this.vertices[index] = value;
                this.uv[index] = uv;
                return value;
            }

            public int[] triangles
            {
                get
                {
                    int counter1 = 0;
                    int trianglesInTop = sides;
                    int trianglesInBottom = sides;
                    int trianglesInSide = (sides * 2) * (Math.Max(levels - 1, 0));
                    int trianglesInFlange = (sides * 2);
                    int totalTriangles = trianglesInTop + trianglesInSide + trianglesInFlange + trianglesInBottom;

                    Debug.Log("Triangles in top: " + trianglesInTop + " Triangles in side: " + trianglesInSide + " Triangles in flange: " + trianglesInFlange + " Triangles in bottom: " + trianglesInBottom);

                    Debug.Log("Allocating storage space for "+totalTriangles +" triangles.");

                    int[] triangles = new int[3 * totalTriangles]; // TODO this does not take into account that we need 2 triangles for each level

                    Debug.Log("Setting triangles for the top");
                    // set up the triangles for the top.
                    for (int counter2 = 0; counter2 < this.sides; counter2++)
                    {
                        triangles[counter1] = this.GetTopIdx();
                        counter1++;
                        triangles[counter1] = this.GetTopEdgeIdx(counter2);
                        counter1++;
                        triangles[counter1] = this.GetTopEdgeIdx((counter2+1)%this.sides);
                        counter1++;                    
                    }

                    Debug.Log("Setting triangles for the sides");
                    // Now loop through all of the levels (execpt for the final level setting up two sets of triangles between that level and the level below it.
                    for (int levelCounter = 0; levelCounter < this.levels - 1; levelCounter++)
                    {
                        for (int sideCounter = 0; sideCounter < this.sides; sideCounter++)
                        {
                            triangles[counter1] = this.GetLevelEdgeIdx(sideCounter, levelCounter);
                            counter1++;
                            triangles[counter1] = this.GetLevelEdgeIdx(sideCounter + 1, levelCounter);
                            counter1++;
                            triangles[counter1] = this.GetLevelEdgeIdx(sideCounter, levelCounter + 1);
                            counter1++;

                            triangles[counter1] = this.GetLevelEdgeIdx(sideCounter + 1, levelCounter);
                            counter1++;
                            triangles[counter1] = this.GetLevelEdgeIdx(sideCounter + 1, levelCounter + 1);
                            counter1++;
                            triangles[counter1] = this.GetLevelEdgeIdx(sideCounter, levelCounter + 1);
                            counter1++;                        
                        }
                    }

                    Debug.Log("Setting triangles for the flange");
                    // Set up the triangles in the flange.
                    for (int sideCounter = 0; sideCounter < this.sides; sideCounter++)
                    {
                        triangles[counter1] = this.GetFlangeTopIdx(sideCounter);
                        counter1++;
                        triangles[counter1] = this.GetFlangeBottomIdx(sideCounter);
                        counter1++;
                        triangles[counter1] = this.GetFlangeTopIdx(sideCounter + 1);
                        counter1++;

                        triangles[counter1] = this.GetFlangeTopIdx(sideCounter + 1);
                        counter1++;
                        triangles[counter1] = this.GetFlangeBottomIdx(sideCounter);
                        counter1++;
                        triangles[counter1] = this.GetFlangeBottomIdx(sideCounter + 1);
                        counter1++;
                    }

                    Debug.Log("Setting triangles for the bottom");
                    // set up the triangles for the bottom.
                    for (int counter2 = 0; counter2 < this.sides; counter2++)
                    {
                        triangles[counter1] = this.GetBottomIdx();
                        counter1++;
                        triangles[counter1] = this.GetBottomEdgeIdx((counter2 + 1) % this.sides);
                        counter1++;
                        triangles[counter1] = this.GetBottomEdgeIdx(counter2);
                        counter1++;
                    }

                    return triangles;
                }
            }
        }

        // A class to hold all the vertices in a fairing that can have a variable number of vertical slices.
        // TODO rewrite this class.
        private class FairingBuilder
        {
            public Vector3[] vertices;
            public Vector2[] uv;
            private int sides;
            private int levels;
            private int insideTopStartIdx;
            private int insideLvlStartIdx;
            private int insideBottomStartIdx;
            private int outsideTopStartIdx;
            private int outsideLvlStartIdx;
            private int outsideBottomStartIdx;
            private int verticesInLevel;

            public FairingBuilder(int _sides, int _levels)
            {
                this.sides = _sides;
                this.levels = _levels;

                this.verticesInLevel = this.sides+1;

                int verticesInInside = verticesInLevel * (levels + 2);
                int verticesInOutside = verticesInLevel * (levels + 2);
                int vertexCount = verticesInInside + verticesInOutside;

                Debug.Log("Allocating space for " + vertexCount + " vertices.");

                vertices = new Vector3[vertexCount];
                uv = new Vector2[vertexCount];

                this.insideTopStartIdx = 0;
                this.insideLvlStartIdx = this.insideTopStartIdx+ verticesInLevel;
                this.insideBottomStartIdx = this.insideLvlStartIdx+ (verticesInLevel*this.levels);

                this.outsideTopStartIdx = verticesInInside;
                this.outsideLvlStartIdx = this.outsideTopStartIdx+ verticesInLevel;
                this.outsideBottomStartIdx = this.outsideLvlStartIdx+ (verticesInLevel*this.levels);

                Debug.Log("insideTopStartIdx:" + insideTopStartIdx + "insideLvlStartIdx:" + insideLvlStartIdx + "insideBottomStartIdx:" + insideBottomStartIdx + "outsideTopStartIdx:" + outsideTopStartIdx + "outsideLvlStartIdx:" + outsideLvlStartIdx + "outsideBottomStartIdx:" + outsideBottomStartIdx);
            }

            public int GetInsideTopIdx(int s)
            {
                return (insideTopStartIdx+s);
            }

            public int GetInsideBottomIdx(int s)
            {
                return (insideBottomStartIdx+s);
            }

            public int GetInsideLevelIdx(int s, int l)
            {
                return (insideLvlStartIdx + (l*this.verticesInLevel) +s);
            }

            public int GetOutsideTopIdx(int s)
            {
                return (outsideTopStartIdx + s);
            }

            public int GetOutsideBottomIdx(int s)
            {
                return (outsideBottomStartIdx + s);
            }

            public int GetOutsideLevelIdx(int s, int l)
            {
                return (outsideLvlStartIdx + (l * this.verticesInLevel) +s);
            }

            public Vector3 SetInsideTop(int s, Vector3 value, Vector2 uv)
            {
                int index = GetInsideTopIdx(s);
                this.vertices[index] = value;
                this.uv[index] = uv;
                return (value);
            }

            public Vector3 SetInsideBottom(int s, Vector3 value, Vector2 uv)
            {
                int index = GetInsideBottomIdx(s);
                this.vertices[index] = value;
                this.uv[index] = uv;
                return (value);
            }

            public Vector3 SetInsideLevel(int s, int l, Vector3 value, Vector2 uv)
            {
                int index = GetInsideLevelIdx(s,l);
                this.vertices[index] = value;
                this.uv[index] = uv;
                return (value);
            }


            public Vector3 SetOutsideTop(int s, Vector3 value, Vector2 uv)
            {
                int index = GetOutsideTopIdx(s);
                this.vertices[index] = value;
                this.uv[index] = uv;
                return (value);
            }

            public Vector3 SetOutsideBottom(int s, Vector3 value, Vector2 uv)
            {
                int index = GetOutsideBottomIdx(s);
                this.vertices[index] = value;
                this.uv[index] = uv;
                return (value);
            }

            public Vector3 SetOutsideLevel(int s, int l, Vector3 value, Vector2 uv)
            {
                int index = GetOutsideLevelIdx(s, l);
                this.vertices[index] = value;
                this.uv[index] = uv;
                return (value);
            }

            public int[] triangles
            {
                get
                {
                    int counter1 = 0;

                    int trianglesInInside = ((this.levels + 1) * 2) * this.sides;
                    int trianglesInOutside = ((this.levels + 1) * 2) * this.sides;
                    int totalTriangles = trianglesInInside + trianglesInOutside;

                    Debug.Log("Triangles in inside: " + trianglesInInside + " Triangles in outside: " + trianglesInOutside);
                    Debug.Log("Allocating storage space for " + totalTriangles + " triangles.");

                    int[] triangles = new int[3 * totalTriangles]; // TODO this does not take into account that we need 2 triangles for each level

                    /*
                    Debug.Log("Setting triangles for the Inside Top. counter1:" + counter1);
                    // Set up the triangles in the inside Top.
                    for (int sideCounter = 0; sideCounter < this.sides; sideCounter++)
                    {
                        triangles[counter1] = this.GetInsideTopIdx(sideCounter);
                        counter1++;
                        triangles[counter1] = this.GetInsideTopIdx(sideCounter + 1);
                        counter1++;
                        triangles[counter1] = this.GetInsideLevelIdx(sideCounter, 0);
                        counter1++;

                        triangles[counter1] = this.GetInsideTopIdx(sideCounter + 1);
                        counter1++;
                        triangles[counter1] = this.GetInsideLevelIdx(sideCounter + 1,0);
                        counter1++;
                        triangles[counter1] = this.GetInsideLevelIdx(sideCounter, 0);
                        counter1++;
                    }

                    Debug.Log("Setting triangles for the Inside sides. counter1:" + counter1);
                    // Now loop through all of the levels (execpt for the final level setting up two sets of triangles between that level and the level below it.
                    for (int levelCounter = 0; levelCounter < this.levels - 1; levelCounter++)
                    {
                        for (int sideCounter = 0; sideCounter < this.sides; sideCounter++)
                        {
                            triangles[counter1] = this.GetInsideLevelIdx(sideCounter, levelCounter);
                            counter1++;
                            triangles[counter1] = this.GetInsideLevelIdx(sideCounter, levelCounter + 1);
                            counter1++;
                            triangles[counter1] = this.GetInsideLevelIdx(sideCounter + 1, levelCounter);
                            counter1++;

                            triangles[counter1] = this.GetInsideLevelIdx(sideCounter + 1, levelCounter);
                            counter1++;
                            triangles[counter1] = this.GetInsideLevelIdx(sideCounter, levelCounter + 1);
                            counter1++;
                            triangles[counter1] = this.GetInsideLevelIdx(sideCounter + 1, levelCounter + 1);
                            counter1++;
                        }
                    }

                    Debug.Log("Setting triangles for the Inside Bottom. counter1:" + counter1);
                    // Set up the triangles in the inside Bottom.
                    for (int sideCounter = 0; sideCounter < this.sides; sideCounter++)
                    {
                        triangles[counter1] = this.GetInsideLevelIdx(sideCounter,this.levels-1);
                        counter1++;
                        triangles[counter1] = this.GetInsideLevelIdx(sideCounter + 1, this.levels - 1);
                        counter1++;
                        triangles[counter1] = this.GetInsideBottomIdx(sideCounter);
                        counter1++;

                        triangles[counter1] = this.GetInsideLevelIdx(sideCounter + 1, this.levels - 1);
                        counter1++;
                        triangles[counter1] = this.GetInsideBottomIdx(sideCounter+1);
                        counter1++;
                        triangles[counter1] = this.GetInsideBottomIdx(sideCounter);
                        counter1++;
                    }
                    

                    Debug.Log("Setting triangles for the Outside Top. counter1:" + counter1);
                    // Set up the triangles in the outside Top.
                    for (int sideCounter = 0; sideCounter < this.sides; sideCounter++)
                    {
                        triangles[counter1] = this.GetOutsideTopIdx(sideCounter);
                        counter1++;
                        triangles[counter1] = this.GetOutsideTopIdx(sideCounter + 1);
                        counter1++;
                        triangles[counter1] = this.GetOutsideLevelIdx(sideCounter, 0);
                        counter1++;

                        triangles[counter1] = this.GetOutsideTopIdx(sideCounter + 1);
                        counter1++;
                        triangles[counter1] = this.GetOutsideLevelIdx(sideCounter + 1, 0);
                        counter1++;
                        triangles[counter1] = this.GetOutsideLevelIdx(sideCounter, 0);
                        counter1++;
                    }
                    */
                    Debug.Log("Setting triangles for the Outside sides. counter1:" + counter1);
                    // Now loop through all of the levels (execpt for the final level setting up two sets of triangles between that level and the level below it.
                    for (int levelCounter = 0; levelCounter < this.levels - 1; levelCounter++)
                    {
                        for (int sideCounter = 0; sideCounter < this.sides; sideCounter++)
                        {
                            triangles[counter1] = this.GetOutsideLevelIdx(sideCounter, levelCounter);
                            counter1++;
                            triangles[counter1] = this.GetOutsideLevelIdx(sideCounter + 1, levelCounter);
                            counter1++;
                            triangles[counter1] = this.GetOutsideLevelIdx(sideCounter, levelCounter + 1);
                            counter1++;

                            triangles[counter1] = this.GetOutsideLevelIdx(sideCounter + 1, levelCounter);
                            counter1++;
                            triangles[counter1] = this.GetOutsideLevelIdx(sideCounter + 1, levelCounter + 1);
                            counter1++;
                            triangles[counter1] = this.GetOutsideLevelIdx(sideCounter, levelCounter + 1);
                            counter1++;
                        }
                    }
                    /*
                    Debug.Log("Setting triangles for the Outside Bottom. counter1:" + counter1);
                    // Set up the triangles in the outside Bottom.
                    for (int sideCounter = 0; sideCounter < this.sides; sideCounter++)
                    {
                        triangles[counter1] = this.GetOutsideLevelIdx(sideCounter, this.levels - 1);
                        counter1++;
                        triangles[counter1] = this.GetOutsideLevelIdx(sideCounter + 1, this.levels - 1);
                        counter1++;
                        triangles[counter1] = this.GetOutsideBottomIdx(sideCounter);
                        counter1++;

                        triangles[counter1] = this.GetOutsideLevelIdx(sideCounter + 1, this.levels - 1);
                        counter1++;
                        triangles[counter1] = this.GetOutsideBottomIdx(sideCounter + 1);
                        counter1++;
                        triangles[counter1] = this.GetOutsideBottomIdx(sideCounter);
                        counter1++;
                    }
                    */
                    return triangles;
                }
            }
        }



        [KSPField]
        public string floatNodeKey = "f";
        [KSPField]
        public string nodePatternKey = "p";
        [KSPField]
        public string topNodeSizeKey = "t";
        [KSPField]
        public string BottomNodeSizeKey = "b";

        [KSPField(isPersistant = true)]
        public int nodePattern = -1; // Note that the value -1 is a special case. In this case the maximum number of attachment nodes will be created, but be placed so far away as to be unusable. They need to be present so they can be used by the loading code.

        [KSPField(isPersistant = true)]
        public int topTBNR = 1;

        [KSPField(isPersistant = true)]
        public int bottomTBNR = 1; 

        private double[] mountingPlateBasesShape; // An array of radii that descibes the shape at the base of the mounting plate. This is generated by the method BuildMountingPlateBaseShape()
        private int sides; // The number of sides that the mounting plate and fairing will be made up from. Note that this value represents the size of the mountingPlateBasesShape array.
        private double maxBasePlateRadius; // This is the largest value in the array mountingPlateBaseShape
        private List<NodePattern> nodePatternList; // list of procedurally created AttachNodes
        private List<TempNodeIcon> tempIcons; // a list of the icons that are used to temporarly display the attachment nodes while the node pattern is being changed
        private String nodeIDRoot = "PWBProcNode";
        private int maxProceduralNodes = 20; // THis needs to be the largest number of procedural nodes that are possible.
        private List<TopBottomRadiusAndNode> tbrnList; // List of radii and attachment node sizes to pick from for the top and bottom.
        private double plateFullHeight = 0.1; // This is the distance between the top of the plate (that fits on the bottom of a fuel tank, and the bottom of a plate (that the engines attach to.) 
        private double flangeHeight = 0.1; // This will not change,m but is shared by both the baseplate and the fairing, so keepmit here.

        private double plateSideBottomY=0; // This is the Y component of the bottom of the side of the moungting plate (ie the level of tht top of the flange
        private double plateBottomY = -0.1; // This is the Y component of the bottom of the plate
        [KSPField(isPersistant = true)]
        private double fairingBaseY=-0.1; // this is the Y component of the base of the fairing (and should match the Y component of the float node)
        private double floatNodeY = 0; // TODO what is the best initial value for this?

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

            // Set the current node pattern to be -1 which will cause dummy nodes to be created which are required for the loading code. The node pattern will be changed to something else on load.
            this.nodePattern = -1;

            // Build a list of possible top/bottom sizes
            this.tbrnList = new List<TopBottomRadiusAndNode>();
            this.tbrnList.Add(new TopBottomRadiusAndNode (0.25f, 0));
            this.tbrnList.Add(new TopBottomRadiusAndNode(0.5f, 1));
            this.tbrnList.Add(new TopBottomRadiusAndNode(1.0f, 2));
            this.tbrnList.Add(new TopBottomRadiusAndNode(1.5f, 2));
            this.tbrnList.Add(new TopBottomRadiusAndNode(2.0f, 2));

            // Create dummy nodes
            this.CreateProceduralNodes();

            // TODO remove debugging
            this.LogAttachmentNodesData("OnAwake}");
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

                this.BuildPlateAndFairing();

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
                    else if (Input.GetKey(BottomNodeSizeKey))
                    {
                        OnBottomNodeSizeKey();
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
            BuildPlateAndFairing();

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
            BuildPlateAndFairing();

            // Add new procedural nodes in a new pattern or move existing ones
            CreateProceduralNodes();

            // Create the icons that display the positions of the procedural attachment nodes.
            CreateTempNodeIcons();

        }

        private void OnBottomNodeSizeKey()
        {

            // Change the TBNR
            this.bottomTBNR = (this.bottomTBNR + 1) % this.tbrnList.Count;

            // Now rebuild the mesh for the mounting plate. Note that this might takea long time, so we need to consider if we want to do this here, or if we want to do it in some other place that marks confirmation that the attachment node pattern has been accepted (when the mouseover is removed, or similar)
            // Note that it is in doing this that the plateHeight is calculated, which is needed to position the atachment nodes.
            BuildPlateAndFairing();

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
            Debug.Log("this.nodePattern" + this.nodePattern);
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
            Debug.Log("CreateProceduralNode { x:" + x + " z:" + z +" size:"+size + " seqNum:"+seqNum);

            String nodeID = this.nodeIDRoot + seqNum.ToString();
            AttachNode newNode = null; // This will either be a copy of an existing node at that seqNum, or a new creation. TODO would it not be better just to create it afresh each time?

            // Firstly, does a Procedural Node with this SeqNum already exist?
            AttachNode attachNode = this.part.attachNodes.Find(an => an.id == nodeID);

            // If we did not find a ProceduralNode with this SeqNum then create it
            if (null == attachNode)
            {
                newNode = new AttachNode();
            }
            else
            {
                newNode = this.CopyNode(attachNode);
            }

            newNode.position = new Vector3(x, (float)-this.plateFullHeight, z);
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
                //Debug.Log("Part is connected");
                foreach (AttachNode node in this.part.attachNodes)
                {
                    //Debug.Log("considering a node: " + node.id);
                    if (AttachNode.NodeType.Stack == node.nodeType)
                    {
                        //Debug.Log("found a stack node");
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

                                //Debug.Log("maxd: " + maxd);

                                // Now that we know how far along the normal the attach node needs to be we can place it
                                if (0 < maxd)
                                {
                                    Debug.Log("node.position: " + node.position);
                                    node.position = normal * maxd * -1.0f; // TODO quick multiply by -1.0 here to make things work, but this needs lots of new thought now that the baseplate is being made procedural
                                    this.floatNodeY = node.position.y /(this.part.scaleFactor*this.part.rescaleFactor) ; // We will be building the meshes assuming that the scaling is all set to 1. Since the lowest point in the other parts in in the final scaling, we need to divide through to get the unscaled lowest point. 
                                    Debug.Log("new node.position: " + node.position);
                                }
                            }
                        }
                    }
                }

                // Now rebuild the mesh for the mounting plate. Note that this might takea long time, so we need to consider if we want to do this here, or if we want to do it in some other place that marks confirmation that the attachment node pattern has been accepted (when the mouseover is removed, or similar)
                // Note that it is in doing this that the plateHeight is calculated, which is needed to position the atachment nodes.
                BuildPlateAndFairing();

                // Add new procedural nodes in a new pattern or move existing ones
                CreateProceduralNodes();

                // Create the icons that display the positions of the procedural attachment nodes.
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
            //Debug.Log("refferingPart : " + refferingPartID);
            //Debug.Log("processing the children of: " + _part.ConstructID);

            foreach (Part _childPart in _part.children)
            {
                if (_childPart.ConstructID != refferingPartID) // ensure that the child is not the reffering part
                {
                    //Debug.Log("considering a child part: " + _childPart.ConstructID);
                    AttachNode node = _part.findAttachNodeByPart(_childPart);

                    if (node == null)
                    {
                        //Debug.Log("No attach point - the child part must be surface mounted");
                        float d = ProcessPart(_childPart, _part, normal);
                        if (d > maxd) { maxd = d; }
                    }
                    else
                    {
                        if (AttachNode.NodeType.Stack == node.nodeType && refferingPart == null && !IsNodeProcedural(node)) // if the part is stack mounted and the reffering part of null and we did nit create the attachment node then this must be connected to the stack of our own part.
                        {
                            //Debug.Log("Not considering this part as it is stack mounted to the orginal part via a node other than one created by this plugin");
                        }
                        else
                        {
                            float d = ProcessPart(_childPart, _part, normal);
                            //Debug.Log("d = " + d);
                            if (d > maxd) { maxd = d; }
                        }
                    }
                }
            } // foreach()

            // Also consider the parent
            if (_part.parent != null)
            {
                //Debug.Log("considering the parent part: " + _part.parent.ConstructID);
                if (_part.parent.ConstructID != refferingPartID)
                {
                    AttachNode node = _part.findAttachNodeByPart(_part.parent);

                    if (node == null)
                    {
                        //Debug.Log("No attach point - the parent part must be surface mounted");
                        float d = ProcessPart(_part.parent, _part, normal);
                        if (d > maxd) { maxd = d; }
                    }
                    else
                    {
                        if (AttachNode.NodeType.Stack == node.nodeType && refferingPart == null && !IsNodeProcedural(node)) // if the part is stack mounted and the reffering part of null then this must be connected to the stack of our wn part.
                        {
                            //Debug.Log("Not considering this part as it is stack mounted to the orginal part.");
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
                    //Debug.Log("parent part is the reffering part, so it will not be consdered.");
                }
            }

            //Debug.Log("Leaving ProcessParts, maxd:" + maxd);
            
            return (maxd);
        }

        private float ProcessPart(Part _part, Part refferingPart ,Vector3 normal)
        {
            //Debug.Log("Entering ProcessPart. part:" + _part.name + " constructID: " + _part.ConstructID);
            float maxd = 0;
            // What is the Normal to the plane? 
//            Vector3 normal = part.transform.rotation * Vector3.up;
            Vector3 pointInPlane = part.transform.localToWorldMatrix.MultiplyPoint3x4(Vector3.zero); // use origin as the point in the plane

            //Debug.Log("Normal: " + normal);
            //Debug.Log("pointInPlane: " + pointInPlane);
            // go through all the verticies in the collider mesh of the part and find out the one that is furthest away from the plane.

            MeshCollider mc = _part.collider as MeshCollider;
            BoxCollider bc = _part.collider as BoxCollider;

            if (mc)
            {
                //Debug.Log("This part has a mesh collider");
                foreach (Vector3 v in mc.sharedMesh.vertices)
                {
                    Vector3 vInWorld = mc.transform.localToWorldMatrix.MultiplyPoint3x4(v);
                    //Debug.Log("Considering vertex: " + vInWorld.ToString());
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
                //Debug.LogError("Box collider: center: " + bc.center.ToString() + " size: " + bc.size.ToString());
                float d = bc.center.y - bc.size.y;

                if (d > maxd)
                {
                    //Debug.Log("d: " +d);
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

            //Debug.Log("Leaving ProcessPart. part: " + _part.name + " maxd: " + maxd);

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

        // This method will generate an array of radii that describe the shape of the base of the moutning plate (also the top of the fairing). it is needed to build both the mounting plate and the fairing.
        private double BuildMountingPlateBaseShape()
        {
            // First do some sanity checking
            if (this.nodePattern < 0) { this.nodePattern = 0; } // This might occur if the user changes the top size before choosing a node pattern.

            double topRadius = this.tbrnList[this.topTBNR].radius;

            // Make some decisions - how many faces
            this.sides = 60; // TODO we need to be MUCH smarter about this!

            // Create the array
            this.mountingPlateBasesShape = new double[this.sides];
            
            double maxCrossSectionRadius = topRadius;

            for (int sideCounter = 0; sideCounter < sides; sideCounter++)
            {
                double crossSectionRadius = topRadius;
                double crossSectionAngle = ((double)sideCounter / (double)sides) * (Math.PI * 2);

                foreach (NodeRing ring in this.nodePatternList[this.nodePattern].rings)
                {
                    for (int counter2 = 0; counter2 < ring.nodeCount; counter2++)
                    {
                        double ringAngle = (Math.PI * 2 * ((double)counter2 / (double)ring.nodeCount)) + (double)ring.offsetAngle;
                        double theta = crossSectionAngle - ringAngle;
                        //Debug.Log("theta:" + theta);
                        double result = GetMaxDistanceFromPlateCentre(theta, ring.radius, ring.nodeRadius);
                        if (!double.IsNaN(result))
                        {
                            if (result > crossSectionRadius)
                            {
                                crossSectionRadius = result;
                            }
                            crossSectionRadius = Math.Max(crossSectionRadius, result);
                        }
                    }
                }

                this.mountingPlateBasesShape[sideCounter] = crossSectionRadius;
                maxCrossSectionRadius = Math.Max(maxCrossSectionRadius, crossSectionRadius);

                // TODO remove debugging
                //Debug.Log("mountingplateBasesShape[" + sideCounter + "]=" + crossSectionRadius.ToString("F3"));
            }
            return (maxCrossSectionRadius);
        }

        private void BuildPlateAndFairing()
        {
            // TODO remove debugging
            // Debug.Log("scaleFactor:" + part.scaleFactor + " RescaleFactor:" + part.rescaleFactor);

            // First do some sanity checking
            if (this.nodePattern < 0) { this.nodePattern = 0; } // This might occur if the user changes the top size before choosing a node pattern.

            // Build the shape at the base of the mounting plate. This will also be used bu the fairing
            this.maxBasePlateRadius = this.BuildMountingPlateBaseShape();

            // Build the mounting plate mesh
            this.BuildMountingPlateMesh();

            // Build the fairing mesh
            this.BuildFairingMesh();
        }

        private void BuildMountingPlateMesh()
        {
            // Make some decisions - how many levels etc
            int levels = 2;  // This represents how many rows of vertices the shaped side walls of the base will be.
            double topRadius = this.tbrnList[this.topTBNR].radius;

            // Get hold of the mesh so we can edit it,
            MeshFilter mf = part.FindModelComponent<MeshFilter>("model");
            if (!mf) { Debug.LogError("[PWB FloatNode] no model for the engine mounting1", part); return; }

            // Sort out the rotations and position so that the mesh matches the part.
            mf.transform.position = part.transform.position;
            mf.transform.rotation = part.transform.rotation;

            Mesh m = mf.mesh;
            if (!m) { Debug.LogError("[PWB FloatNode] no model for the engine mounting2", part); return; }

            PlateBuilder builder; // This will be created later once we know all the information needed to pass into its constructor

            double sideHeight = Math.Max(maxBasePlateRadius - topRadius, 0);// TODO Add a multiple in here so we can determine the slope
            this.plateSideBottomY = -sideHeight;
            this.plateFullHeight = sideHeight + flangeHeight;
            this.plateBottomY = -plateFullHeight;
            // Now that we know how high the sides are going to do, we can make a  decision about how many levels we will need.
            if (0.0 == sideHeight)
            {
                levels = 0; // If the side height is zero, we do not need any levels, the part will be made up of just the flange.
            }
            else
            {
                levels = (int)(Math.Ceiling(sideHeight / 0.1)) + 1;
            }

            // TODO remove debugging
            Debug.Log("radius:" + topRadius + "\nmaxCrossSectionRadius:" + this.maxBasePlateRadius + "\nsideHeight:" + sideHeight + "\nflangeHeight:" + flangeHeight + "\nthis.plateFullHeight:" + this.plateFullHeight + "\nlevels:" + levels + "\nsides:" + sides);

            // We now know everything we need to know to allocate the builder.
            builder = new PlateBuilder(sides, levels);

            // The top plate
            {
                // The central vertex in the center of the plate
                builder.SetTop(Vector3d.zero, new Vector2(0.25f, 0.75f));

                // the ring of outside vertices at the top.
                // TODO debug remove
                Debug.Log("setting out the top ring of vertices");
                for (int counter1 = 0; counter1 < sides; counter1++)
                {
                    double angle = ((double)counter1 / (double)sides) * (2 * Math.PI);
                    Vector3 vertex = new Vector3((float)(Math.Sin(angle) * topRadius), 0, (float)(Math.Cos(angle) * topRadius));
                    Vector2 uvTop = new Vector2(0.25f + (float)(Math.Sin(angle) * 0.2), 0.75f + (float)(Math.Cos(angle) * 0.2));
                    builder.SetTopEdge(counter1, vertex, uvTop);
                }
            }

            // Now do the sides
            if (levels > 0) // If levels was set to 0 because there are not sides then we need  not do any of this.
            {
                // First thing to do constructing the sides is to set up an array for Bezier curves that represent the profile through the plate at various angles. Hence the co-ordinates for each curve are not x&y but height&radius.
                BezierCurve[] curves = new BezierCurve[sides];
                for (int counter = 0; counter < sides; counter++)
                {
                    Vector2 startPoint = new Vector2((float)topRadius, 0.0f);
                    Vector2 startControl = new Vector2((float)topRadius, (float)plateSideBottomY * 0.5f);
                    Vector2 endControl = new Vector2((float)this.mountingPlateBasesShape[counter], (float)plateSideBottomY * 0.5f);
                    Vector2 endPoint = new Vector2((float)this.mountingPlateBasesShape[counter], (float)plateSideBottomY);

                    curves[counter] = new BezierCurve(startPoint, startControl, endControl, endPoint);
                }

                // Now we can use the Bezier Curves to generate the vertices for the sides. Hopefully we will get nice smooth curves (lovely).
                for (int levelCounter = 0; levelCounter < levels; levelCounter++)
                {
                    float i = (float)levelCounter / ((float)levels - 1f);

                    for (int sideCounter = 0; sideCounter < sides + 1; sideCounter++)
                    {
                        Vector2 pointInCurve = curves[sideCounter % sides].PointInCurve(i); // This gets height / radius co-ordinates

                        double angle = ((double)sideCounter / (double)sides) * (2 * Math.PI);
                        Vector3 vertex = new Vector3((float)(Math.Sin(angle) * pointInCurve.x), pointInCurve.y, (float)(Math.Cos(angle) * pointInCurve.x));
                        Vector2 uvEdge = new Vector2(0.05f + ((0.9f * (float)sideCounter) / (float)sides), Mathf.Lerp(0.05f, 0.45f, i));
                        builder.SetLevelEdge(sideCounter, levelCounter, vertex, uvEdge);
                    }
                }
            }

            // Create the flange under the sides.
            for (int sideCounter = 0; sideCounter < sides + 1; sideCounter++)
            {
                double angle = ((double)sideCounter / (double)sides) * (2 * Math.PI);
                Vector3 vertexTop = new Vector3((float)(Math.Sin(angle) * this.mountingPlateBasesShape[sideCounter % sides]), (float)-sideHeight, (float)(Math.Cos(angle) * this.mountingPlateBasesShape[sideCounter % sides]));
                Vector2 uvTop = new Vector2(0.05f + ((0.9f * (float)sideCounter) / (float)sides), 0.45f);
                builder.SetFlangeTop(sideCounter, vertexTop, uvTop);

                Vector3 vertexBottom = new Vector3((float)(Math.Sin(angle) * this.mountingPlateBasesShape[sideCounter % sides]), (float)-this.plateFullHeight, (float)(Math.Cos(angle) * this.mountingPlateBasesShape[sideCounter % sides]));
                Vector2 uvBottom = new Vector2(0.05f + ((0.9f * (float)sideCounter) / (float)sides), 0.05f);
                builder.SetFlangeBottom(sideCounter, vertexBottom, uvBottom);
            }

            // the ring of outside vertices at the bottom.
            // TODO debug remove
            Debug.Log("setting out the bottom ring of vertices");

            {
                for (int sideCounter = 0; sideCounter < sides; sideCounter++)
                {
                    double angle = ((double)sideCounter / (double)sides) * (2 * Math.PI);
                    Vector3 vertex = new Vector3((float)(Math.Sin(angle) * this.mountingPlateBasesShape[sideCounter % sides]), (float)-this.plateFullHeight, (float)(Math.Cos(angle) * this.mountingPlateBasesShape[sideCounter % sides]));
                    Vector2 uvBottom = new Vector2(0.75f + (float)(Math.Sin(angle) * 0.2), 0.75f + (float)(Math.Cos(angle) * 0.2));
                    builder.SetBottomEdge(sideCounter, vertex, uvBottom);
                }

                // Set up the bottom centre vertex.
                builder.SetBottom(new Vector3(0, (float)-this.plateFullHeight, 0), new Vector2(0.75f, 0.75f));
            }

            // Now set up all the vertices triangles etc into the mesh
            {
                m.Clear();
                m.vertices = builder.vertices;
                m.normals = new Vector3[m.vertexCount];
                m.triangles = builder.triangles;
                m.uv = builder.uv;
                m.RecalculateNormals();
                calculateMeshTangents(m);
                
                // TODO does this work?
                // calculateMeshTangents(m); // Attempt to calculate mesh tangents

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

        // This function builds the mesh and collision mesh for the fairing.
        private void BuildFairingMesh()
        {
            // Make some decisions - how many levels etc
            int levels = 2;  // This represents how many rows of vertices the shaped side walls of the base will be.

            double bottomRadius = this.tbrnList[this.bottomTBNR].radius;

            this.fairingBaseY = Math.Min(this.floatNodeY, this.plateBottomY); // If the floatnode is in a silly location (ie above the bottom of the plate) then correct for this.

            Debug.Log("fairingBaseY: " + this.fairingBaseY + "\nfloatNodeY: " + this.floatNodeY + "\nthis.plateBottomY: " + this.plateBottomY + "\nthis.plateSideBottomY: " + this.plateSideBottomY);

            // Get hold of the mesh so we can edit it,
            MeshFilter mf = part.FindModelComponent<MeshFilter>("fairing");
            if (!mf) { Debug.LogError("[PWB Procedural Engine Housing] no model for the fairing1", part); return; }

            // Sort out the rotations and position so that the mesh matches the part.
            mf.transform.position = part.transform.position;
            mf.transform.rotation = part.transform.rotation;

            Mesh m = mf.mesh;
            if (!m) { Debug.LogError("[PWB Procedural Engine Housing] no model for the fairing2", part); return; }

            FairingBuilder builder; // This will be created later once we know all the information needed to pass into its constructor

            // Work out a few height related things first
            float fairingTopPoint = (float)this.plateBottomY + (float)this.flangeHeight;
            float fairingSideTop = (float)this.plateBottomY;
            float fairingSideBottom = (float)this.fairingBaseY;
            float fairingBottomPoint = (float)this.fairingBaseY - 0.1f;

            Debug.Log("fairingTopPoint:" + fairingTopPoint + "\nfairingSideTop:" + fairingSideTop + "\nfairingSideBottom:" + fairingSideBottom + "\nfairingBottomPoint:" + fairingBottomPoint);

            float fairingHeight = fairingSideTop - fairingSideBottom;
            
            levels = (int)(Math.Ceiling(fairingHeight / 0.1)) + 1;
       
            // TODO remove debugging
            Debug.Log("bottomRadius:" + bottomRadius + "\nmaxBasePlateRadius:" + this.maxBasePlateRadius + "\nfairingHeight:" + fairingHeight  + "\nlevels:" + levels + "\nsides:" + this.sides);

            // We now know everything we need to know to allocate the builder.
            builder = new FairingBuilder(sides, levels);
      
            // First do the top edge
            Debug.Log("top edge");            
            for (int sideCounter = 0; sideCounter < sides + 1; sideCounter++)
            {
                double angle = ((double)sideCounter / (double)sides) * (2 * Math.PI);
                Vector3 vertex = new Vector3((float)(Math.Sin(angle) * this.mountingPlateBasesShape[sideCounter % sides]), fairingTopPoint, (float)(Math.Cos(angle) * this.mountingPlateBasesShape[sideCounter % sides]));
                Vector2 uvInsideTop = new Vector2(0.05f + ((0.9f * (float)sideCounter) / (float)sides), 0.45f); // TODO
                Vector2 uvOutsideTop = new Vector2(0.05f + ((0.9f * (float)sideCounter) / (float)sides), 0.45f); // TODO

                builder.SetInsideTop(sideCounter, vertex, uvInsideTop);
                builder.SetOutsideTop(sideCounter, vertex, uvOutsideTop);
            }

            // Next do the bottom edge
            Debug.Log("bottom edge");
            for (int sideCounter = 0; sideCounter < sides + 1; sideCounter++)
            {
                double angle = ((double)sideCounter / (double)sides) * (2 * Math.PI);
                Vector3 vertex = new Vector3((float)(Math.Sin(angle) * bottomRadius), fairingBottomPoint, (float)(Math.Cos(angle) * bottomRadius));
                Vector2 uvInsideBottom = new Vector2(0.05f + ((0.9f * (float)sideCounter) / (float)sides), 0.05f); // TODO
                Vector2 uvOutsideBottom = new Vector2(0.05f + ((0.9f * (float)sideCounter) / (float)sides), 0.05f); // TODO

                builder.SetInsideBottom(sideCounter, vertex, uvInsideBottom);
                builder.SetOutsideBottom(sideCounter, vertex, uvOutsideBottom);
            }

            // Now do the sides
            Debug.Log("sides");
            if (levels > 0) // If levels was set to 0 because there are not sides then we need  not do any of this. // TODO consider what to do, and where do we set the number of levels.
            {
                // First thing to do constructing the sides is to set up an array for Bezier curves that represent the profile through the plate at various angles. Hence the co-ordinates for each curve are not x&y but height&radius.
                BezierCurve[] insideCurves = new BezierCurve[sides];
                BezierCurve[] outsideCurves = new BezierCurve[sides];

                Debug.Log("curves");
                for (int counter = 0; counter < sides; counter++)
                {
                    Vector2 insideStartPoint = new Vector2((float)this.mountingPlateBasesShape[counter], fairingSideTop);
                    Vector2 insideStartControl = new Vector2((float)this.mountingPlateBasesShape[counter], fairingSideTop - (0.25f * (fairingHeight)));
                    Vector2 insideEndControl = new Vector2((float)bottomRadius, fairingSideBottom + (0.25f * (fairingHeight)));
                    Vector2 insideEndPoint = new Vector2((float)bottomRadius, fairingSideBottom);

                    insideCurves[counter] = new BezierCurve(insideStartPoint, insideStartControl, insideEndControl, insideEndPoint);

                    Vector2 outsideStartPoint = new Vector2((float)this.mountingPlateBasesShape[counter] + 0.05f, fairingSideTop);
                    Vector2 outsideStartControl = new Vector2((float)this.mountingPlateBasesShape[counter] + 0.05f, fairingSideTop - (0.25f * (fairingHeight)));
                    Vector2 outsideEndControl = new Vector2((float)bottomRadius + 0.05f, fairingSideBottom + (0.25f * (fairingHeight)));
                    Vector2 outsideEndPoint = new Vector2((float)bottomRadius + 0.05f, fairingSideBottom);

                    outsideCurves[counter] = new BezierCurve(outsideStartPoint, outsideStartControl, outsideEndControl, outsideEndPoint);
                }

                Debug.Log("points");
                // Now we can use the Bezier Curves to generate the vertices for the sides. Hopefully we will get nice smooth curves (lovely).
                for (int levelCounter = 0; levelCounter < levels; levelCounter++)
                {
                    float i = (float)levelCounter / ((float)levels - 1f);

                    // Do the inside vertices
                    for (int sideCounter = 0; sideCounter < sides + 1; sideCounter++)
                    {
                        Vector2 pointInCurve = insideCurves[sideCounter % sides].PointInCurve(i); // This gets height / radius co-ordinates

                        double angle = ((double)sideCounter / (double)sides) * (2 * Math.PI);
                        Vector3 vertex = new Vector3((float)(Math.Sin(angle) * pointInCurve.x), pointInCurve.y, (float)(Math.Cos(angle) * pointInCurve.x));
                        Vector2 uvEdge = new Vector2(0.05f + ((0.9f * (float)sideCounter) / (float)sides), Mathf.Lerp(0.05f, 0.45f, i)); // TODO sort this lot out!
                        builder.SetInsideLevel(sideCounter, levelCounter, vertex, uvEdge);
                    }

                    // Now do the outside vertices
                    for (int sideCounter = 0; sideCounter < sides + 1; sideCounter++)
                    {
                        Vector2 pointInCurve = outsideCurves[sideCounter % sides].PointInCurve(i); // This gets height / radius co-ordinates

                        double angle = ((double)sideCounter / (double)sides) * (2 * Math.PI);
                        Vector3 vertex = new Vector3((float)(Math.Sin(angle) * pointInCurve.x), pointInCurve.y, (float)(Math.Cos(angle) * pointInCurve.x));
                        Vector2 uvEdge = new Vector2(0.05f + ((0.9f * (float)sideCounter) / (float)sides), Mathf.Lerp(0.05f, 0.45f, i)); // TODO sort this lot out!
                        builder.SetOutsideLevel(sideCounter, levelCounter, vertex, uvEdge);
                    }
                }
            }

            // Now set up all the vertices triangles etc into the mesh
            {
                m.Clear();
                m.vertices = builder.vertices;
                m.normals = new Vector3[m.vertexCount];
                m.triangles = builder.triangles;
                m.uv = builder.uv;
                m.RecalculateNormals();
                calculateMeshTangents(m);

                // TODO is it a good idea optimize in the editor - does it cause a performance problem?
                //if (!HighLogic.LoadedSceneIsEditor) m.Optimize();
                m.Optimize();
            }

            // Finally set the collider mesh to use this new mesh  TODO what do we need to do here?
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

        // code borrowed from http://answers.unity3d.com/questions/7789/calculating-tangents-vector4.html to recalculate thte tangents for the mesh
        public static void calculateMeshTangents(Mesh mesh)
        {
            //speed up math by copying the mesh arrays    
            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;
            Vector2[] uv = mesh.uv;
            Vector3[] normals = mesh.normals;
            //variable definitions
            int triangleCount = triangles.Length;
            int vertexCount = vertices.Length;
            Vector3[] tan1 = new Vector3[vertexCount];
            Vector3[] tan2 = new Vector3[vertexCount];
            Vector4[] tangents = new Vector4[vertexCount];
            for (long a = 0; a < triangleCount; a += 3)
            {
                long i1 = triangles[a + 0];
                long i2 = triangles[a + 1];
                long i3 = triangles[a + 2];
                Vector3 v1 = vertices[i1];
                Vector3 v2 = vertices[i2];
                Vector3 v3 = vertices[i3];
                Vector2 w1 = uv[i1];
                Vector2 w2 = uv[i2];
                Vector2 w3 = uv[i3];
                float x1 = v2.x - v1.x;
                float x2 = v3.x - v1.x;
                float y1 = v2.y - v1.y;
                float y2 = v3.y - v1.y;
                float z1 = v2.z - v1.z;
                float z2 = v3.z - v1.z;
                float s1 = w2.x - w1.x;
                float s2 = w3.x - w1.x;
                float t1 = w2.y - w1.y;
                float t2 = w3.y - w1.y;
                float r = 1.0f / (s1 * t2 - s2 * t1);
                Vector3 sdir = new Vector3((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
                Vector3 tdir = new Vector3((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);
                tan1[i1] += sdir;
                tan1[i2] += sdir;
                tan1[i3] += sdir;
                tan2[i1] += tdir;
                tan2[i2] += tdir;
                tan2[i3] += tdir;
            }
            for (long a = 0; a < vertexCount; ++a)
            {
                Vector3 n = normals[a];
                Vector3 t = tan1[a];
                //Vector3 tmp = (t - n * Vector3.Dot(n, t)).normalized;
                //tangents[a] = new Vector4(tmp.x, tmp.y, tmp.z);
                Vector3.OrthoNormalize(ref n, ref t);
                tangents[a].x = t.x;
                tangents[a].y = t.y;
                tangents[a].z = t.z;
                tangents[a].w = (Vector3.Dot(Vector3.Cross(n, t), tan2[a]) < 0.0f) ? -1.0f : 1.0f;
            }
            mesh.tangents = tangents;
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
