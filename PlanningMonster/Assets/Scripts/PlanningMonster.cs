// Planning Monster: a motion planning project of GRA class
// Weng, Wei-Chen 2017/11/21
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System;
using UnityEngine;
using UnityEngine.UI;

public class PlanningMonster : MonoBehaviour
{
    //-------------public-------------
    public Camera mainCamera;
    public Material robotMaterial;
    public Material obsMaterial;
    public Material bgMaterial;
    public Text calculateTime;

    //-------------private-------------
    Stopwatch stopWatch = new Stopwatch();
    List<string> robotDat = new List<string>();
    List<string> obstaclesDat = new List<string>();
    List<string> deleteList = new List<string>();
    Robot[] robot;
    Obstacles[] obstacles;
    float objDepth = 10.0f;  // Screen space
    float bgDepth = 300.0f;  // Screen space
    int scale = 3;
    int backgroundSize = 128;
    Texture2D backgroundTexture;
    Texture2D pathTexture;
    Texture2D[] pfTexture = new Texture2D[2];
    GameObject selectedPoly;
    bool moving = false;
    Vector3 mouseStartPosition;
    Vector3 mouseCurrPosition;
    Vector3 polyStartPosition;
    Vector3 backgroundOffset;
    Vector3 moveOffset;
    Vector3 rotateStartVector;
    Vector3 rotateCurrVector;

    //-------------struct-------------
    public struct Configuration
    {
        public Vector3 position;
        public float rotation;
    }
    public struct Polygon
    {
        public int numberOfVertices;
        public Vector3[] vertices;
    }
    //-------------class-------------
    public class Robot
    {
        public int numberOfPolygon;
        public Polygon[] polygon;
        public Configuration initial;
        public Configuration goal;
        public int numberOfControlPoint;
        public Vector3[] controlPoint;
        public Robot() { }
    }
    public class Obstacles
    {
        public int numberOfPolygon;
        public Polygon[] polygon;
        public Configuration initial;
        public Obstacles() { }
    }

    // Use this for initialization
    void Start()
    {
        // var initialization
        backgroundOffset = new Vector3((float)Screen.width / 7, ((float)Screen.height - backgroundSize * scale) / 2, 0f);
        backgroundTexture = new Texture2D(backgroundSize, backgroundSize);
        pathTexture = new Texture2D(backgroundSize, backgroundSize);
        for (int i = 0; i < pfTexture.GetLength(0); i++)
            pfTexture[i] = new Texture2D(backgroundSize, backgroundSize);

        // Print configuration
        print("Screen width: " + Screen.width + ", height: " + Screen.height);
        print("Background Size: " + backgroundSize);

        // Load data
        loadData();

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButton(0))    // Mouse Left-button : Translate
        {
            RaycastHit hitInfo = new RaycastHit();
            bool hit = Physics.Raycast(mainCamera.ScreenPointToRay(Input.mousePosition), out hitInfo);
            if (hit)
            {
                //Debug.Log("Hit " + hitInfo.transform.parent.gameObject.name);
                if (!moving)    // First hit: decide offset
                {
                    selectedPoly = GameObject.Find(hitInfo.transform.parent.gameObject.name);
                    mouseStartPosition = new Vector3(Input.mousePosition.x, Input.mousePosition.y, objDepth);
                    polyStartPosition = mainCamera.WorldToScreenPoint(selectedPoly.transform.position);
                    moveOffset = polyStartPosition - mouseStartPosition;

                    moving = true;
                }
            }
            else
            {
                //Debug.Log("No hit");
            }
            if (moving)   // Start moving: polygon position = mouse current position + offset
            {
                mouseCurrPosition = new Vector3(Input.mousePosition.x, Input.mousePosition.y, objDepth);
                selectedPoly.transform.position = mainCamera.ScreenToWorldPoint(mouseCurrPosition + moveOffset);
            }
        }
        if (Input.GetMouseButton(1))    // Mouse Right-button : Rotation
        {
            RaycastHit hitInfo = new RaycastHit();
            bool hit = Physics.Raycast(mainCamera.ScreenPointToRay(Input.mousePosition), out hitInfo);
            if (hit)
            {
                //Debug.Log("Hit " + hitInfo.transform.parent.gameObject.name);
                if (!moving) // First hit: decide selectedPoly
                {
                    selectedPoly = GameObject.Find(hitInfo.transform.parent.gameObject.name);
                    mouseStartPosition = new Vector3(Input.mousePosition.x, Input.mousePosition.y, objDepth);
                    rotateStartVector = (mouseStartPosition - mainCamera.WorldToScreenPoint(selectedPoly.transform.position)).normalized;

                    moving = true;
                }
            }
            else
            {
                //Debug.Log("No hit");
            }
            if (moving)
            {
                mouseCurrPosition = new Vector3(Input.mousePosition.x, Input.mousePosition.y, objDepth);
                rotateCurrVector = (mouseCurrPosition - mainCamera.WorldToScreenPoint(selectedPoly.transform.position));

                float rotateAngle = Vector3.Angle(rotateStartVector, rotateCurrVector);
                Vector3 getDirect = Vector3.Cross(rotateStartVector, rotateCurrVector).normalized;
                if (getDirect.z < 0)
                    rotateAngle = -rotateAngle;
                selectedPoly.transform.Rotate(new Vector3(0, 0, 1), rotateAngle, Space.Self);
                rotateStartVector = rotateCurrVector;
                /*float rotateAngle = Vector3.Angle(new Vector3(1, 0, 0), rotateCurrVector);
                Vector3 direction = Vector3.Cross(new Vector3(1, 0, 0), rotateCurrVector);
                if (direction.z < 0)
                {
                    rotateAngle = -rotateAngle + 360.0f;
                }
                if (rotateAngle >= 360.0f)
                    rotateAngle -= 360.0f;
                selectedPoly.transform.rotation = Quaternion.Euler(0, 0, rotateAngle);*/
            }
        }
        if (Input.GetMouseButtonUp(0))
        {
            //  Write back translate
            if (moving)
            {
                string[] selected = selectedPoly.name.Split(' ');
                Vector3 translate = (mouseCurrPosition - mouseStartPosition) / scale;

                if (selected[0] == "robot")
                    robot[Int32.Parse(selected[1])].initial.position += translate;
                if (selected[0] == "obstacle")
                    obstacles[Int32.Parse(selected[1])].initial.position += translate;

                myBug();
            }

            moving = false;
        }
        if (Input.GetMouseButtonUp(1))
        {
            if (moving)
            {
                //  Write back rorate
                string[] selected = selectedPoly.name.Split(' ');

                if (selected[0] == "robot")
                    robot[Int32.Parse(selected[1])].initial.rotation = selectedPoly.transform.rotation.eulerAngles.z;
                if (selected[0] == "obstacle")
                    obstacles[Int32.Parse(selected[1])].initial.rotation = selectedPoly.transform.rotation.eulerAngles.z;

                myBug();
            }

            moving = false;
        }
    }

    public void loadData()
    {
        // Remove polygon & background
        for (int i = 0; i < deleteList.Count; i++)
            Destroy(GameObject.Find(deleteList[i]));
        if (GameObject.Find("Background"))
            Destroy(GameObject.Find("Background"));
        deleteList.Clear();

        // Read data
        readDat(Application.streamingAssetsPath + "/robot.dat", robotDat);
        readDat(Application.streamingAssetsPath + "/obstacles.dat", obstaclesDat);

        // Create background, robots & obstacles data structure
        bgCreate();
        robotCreate();
        obstaclesCreate();

        // Draw robots & obstacles
        for (int rob = 0; rob < robot.GetLength(0); rob++)  // robot
        {
            GameObject parentRobot = new GameObject();
            parentRobot.name = "robot " + rob;
            deleteList.Add(parentRobot.name);
            parentRobot.transform.position = mainCamera.ScreenToWorldPoint(robot[rob].initial.position * scale + backgroundOffset);
            for (int poly = 0; poly < robot[rob].numberOfPolygon; poly++)
            {
                Vector3[] vertices = new Vector3[robot[rob].polygon[poly].numberOfVertices];
                // Load initial configuration
                for (int ver = 0; ver < robot[rob].polygon[poly].numberOfVertices; ver++)
                {
                    vertices[ver].x = robot[rob].polygon[poly].vertices[ver].x + robot[rob].initial.position.x;
                    vertices[ver].y = robot[rob].polygon[poly].vertices[ver].y + robot[rob].initial.position.y;
                    vertices[ver].z = robot[rob].polygon[poly].vertices[ver].z;
                }
                drawPolygon(vertices, parentRobot, robotMaterial, parentRobot.name + " polygon " + poly);
            }
            parentRobot.transform.Rotate(new Vector3(0, 0, 1), robot[rob].initial.rotation, Space.Self);
        }
        for (int obs = 0; obs < obstacles.GetLength(0); obs++)  // obstacle
        {
            GameObject parentObstacle = new GameObject();
            parentObstacle.name = "obstacle " + obs;
            deleteList.Add(parentObstacle.name);
            parentObstacle.transform.position = mainCamera.ScreenToWorldPoint(obstacles[obs].initial.position * scale + backgroundOffset);
            for (int poly = 0; poly < obstacles[obs].numberOfPolygon; poly++)
            {
                Vector3[] vertices = new Vector3[obstacles[obs].polygon[poly].numberOfVertices];
                // Load initial configuration
                for (int ver = 0; ver < obstacles[obs].polygon[poly].numberOfVertices; ver++)
                {
                    vertices[ver].x = obstacles[obs].polygon[poly].vertices[ver].x + obstacles[obs].initial.position.x;
                    vertices[ver].y = obstacles[obs].polygon[poly].vertices[ver].y + obstacles[obs].initial.position.y;
                    vertices[ver].z = obstacles[obs].polygon[poly].vertices[ver].z;
                }
                drawPolygon(vertices, parentObstacle, obsMaterial, parentObstacle.name + " polygon " + poly);
            }
            parentObstacle.transform.Rotate(new Vector3(0, 0, 1), obstacles[obs].initial.rotation, Space.Self);
        }
        myBug();
    }

    public void readDat(string path, List<string> dat)
    {
        StreamReader reader = new StreamReader(path);
        string line;
        string ignoreLine = "#";

        while ((line = reader.ReadLine()) != null)
        {
            if (line.Substring(0, 1) != ignoreLine)
                dat.Add(line);
        }

        reader.Close();
    }

    public void bgCreate()
    {
        Vector3[] vertices = new[] {   // background size
            new Vector3(0f, 0f, bgDepth),
            new Vector3(backgroundSize*scale, 0f, bgDepth),
            new Vector3(backgroundSize*scale, backgroundSize*scale, bgDepth),
            new Vector3(0f, backgroundSize*scale, bgDepth)};
        for (int i = 0; i < vertices.GetLength(0); i++)   // translate offset
            vertices[i] += backgroundOffset;
        for (int i = 0; i < vertices.GetLength(0); i++)   // transform to world
            vertices[i] = mainCamera.ScreenToWorldPoint(vertices[i]);

        // UV
        Vector2[] UV = new[] {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)};

        // Triangles
        int[] triangles = new int[(vertices.Length - 2) * 3];
        for (int i = 0; i < (vertices.Length - 2); i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 2;
            triangles[i * 3 + 2] = i + 1;
        }

        // Mesh 
        Mesh bgMesh = new Mesh();
        bgMesh.vertices = vertices;
        bgMesh.uv = UV;
        bgMesh.name = "bgMesh";
        bgMesh.triangles = triangles;
        bgMesh.RecalculateNormals();
        bgMesh.RecalculateBounds();

        // Texture
        for (int x = 0; x < backgroundSize; x++)
            for (int y = 0; y < backgroundSize; y++)
                backgroundTexture.SetPixel(x, y, new Color(0, 0, 0));
        backgroundTexture.Apply();

        // Main Background
        GameObject mainBG = new GameObject();
        mainBG.name = "Background";
        mainBG.AddComponent<MeshFilter>();
        mainBG.GetComponent<MeshFilter>().mesh = bgMesh;
        mainBG.AddComponent<MeshRenderer>();
        mainBG.GetComponent<MeshRenderer>().material = bgMaterial;
        mainBG.GetComponent<MeshRenderer>().material.mainTexture = backgroundTexture;
    }

    public void robotCreate()
    {
        int lineCount = 0;
        int numberOfRobot = Int32.Parse(robotDat[lineCount++]);
        //print(numberOfRobot);
        robot = new Robot[numberOfRobot];
        for (int robotCount = 0; robotCount < numberOfRobot; robotCount++)
        {
            // Construct robot
            robot[robotCount] = new Robot();
            // Polygon
            robot[robotCount].numberOfPolygon = Int32.Parse(robotDat[lineCount++]);
            //print(robot[robotCount].numberOfPolygon);
            robot[robotCount].polygon = new Polygon[robot[robotCount].numberOfPolygon];
            for (int polygonCount = 0; polygonCount < robot[robotCount].numberOfPolygon; polygonCount++)
            {
                // Vertices
                robot[robotCount].polygon[polygonCount].numberOfVertices = Int32.Parse(robotDat[lineCount++]);
                //print(robot[robotCount].polygon[polygonCount].numberOfVertices);
                robot[robotCount].polygon[polygonCount].vertices = new Vector3[robot[robotCount].polygon[polygonCount].numberOfVertices];
                for (int verticesCount = 0; verticesCount < robot[robotCount].polygon[polygonCount].numberOfVertices; verticesCount++)
                {
                    string[] verticesSplit = robotDat[lineCount++].Split(' ');
                    robot[robotCount].polygon[polygonCount].vertices[verticesCount].x = Convert.ToSingle(verticesSplit[0]);
                    robot[robotCount].polygon[polygonCount].vertices[verticesCount].y = Convert.ToSingle(verticesSplit[1]);
                    robot[robotCount].polygon[polygonCount].vertices[verticesCount].z = objDepth;
                    //print("(" + robot[robotCount].polygon[polygonCount].vertices[verticesCount].x +
                    //", " + robot[robotCount].polygon[polygonCount].vertices[verticesCount].y + ")");
                }
            }
            // Configration
            string[] initialSplit = robotDat[lineCount++].Split(' ');
            robot[robotCount].initial.position.x = Convert.ToSingle(initialSplit[0]);
            robot[robotCount].initial.position.y = Convert.ToSingle(initialSplit[1]);
            robot[robotCount].initial.position.z = objDepth;
            robot[robotCount].initial.rotation = Convert.ToSingle(initialSplit[2]);
            //print(robot[robotCount].initial.position.x + ", " + robot[robotCount].initial.position.y + ", " + robot[robotCount].initial.rotation);
            string[] goalSplit = robotDat[lineCount++].Split(' ');
            robot[robotCount].goal.position.x = Convert.ToSingle(goalSplit[0]);
            robot[robotCount].goal.position.y = Convert.ToSingle(goalSplit[1]);
            robot[robotCount].goal.position.z = objDepth;
            robot[robotCount].goal.rotation = Convert.ToSingle(goalSplit[2]);
            //print(robot[robotCount].goal.position.x + ", " + robot[robotCount].goal.position.y + ", " + robot[robotCount].goal.rotation);
            // Control Point
            robot[robotCount].numberOfControlPoint = Int32.Parse(robotDat[lineCount++]);
            //print(robot[robotCount].numberOfControlPoint);
            robot[robotCount].controlPoint = new Vector3[robot[robotCount].numberOfControlPoint];
            for (int controlPointCount = 0; controlPointCount < robot[robotCount].numberOfControlPoint; controlPointCount++)
            {
                string[] controlPointSplit = robotDat[lineCount++].Split(' ');
                robot[robotCount].controlPoint[controlPointCount].x = Convert.ToSingle(controlPointSplit[0]);
                robot[robotCount].controlPoint[controlPointCount].y = Convert.ToSingle(controlPointSplit[1]);
                robot[robotCount].controlPoint[controlPointCount].z = objDepth;
                //print("(" + robot[robotCount].controlPoint[controlPointCount].x + ", "
                //+ robot[robotCount].controlPoint[controlPointCount].y + ")");
            }
        }
    }

    public void obstaclesCreate()
    {
        int lineCount = 0;
        int numberOfObstacles = Int32.Parse(obstaclesDat[lineCount++]);
        //print(numberOfObstacles);
        obstacles = new Obstacles[numberOfObstacles];
        for (int obstaclesCount = 0; obstaclesCount < numberOfObstacles; obstaclesCount++)
        {
            // Construct obstacles
            obstacles[obstaclesCount] = new Obstacles();
            // Polygon
            obstacles[obstaclesCount].numberOfPolygon = Int32.Parse(obstaclesDat[lineCount++]);
            //print(obstacles[obstaclesCount].numberOfPolygon);
            obstacles[obstaclesCount].polygon = new Polygon[obstacles[obstaclesCount].numberOfPolygon];
            for (int polygonCount = 0; polygonCount < obstacles[obstaclesCount].numberOfPolygon; polygonCount++)
            {
                // Vertices
                obstacles[obstaclesCount].polygon[polygonCount].numberOfVertices = Int32.Parse(obstaclesDat[lineCount++]);
                //print(obstacles[obstaclesCount].polygon[polygonCount].numberOfVertices);
                obstacles[obstaclesCount].polygon[polygonCount].vertices = new Vector3[obstacles[obstaclesCount].polygon[polygonCount].numberOfVertices];
                for (int verticesCount = 0; verticesCount < obstacles[obstaclesCount].polygon[polygonCount].numberOfVertices; verticesCount++)
                {
                    string[] verticesSplit = obstaclesDat[lineCount++].Split(' ');
                    obstacles[obstaclesCount].polygon[polygonCount].vertices[verticesCount].x = Convert.ToSingle(verticesSplit[0]);
                    obstacles[obstaclesCount].polygon[polygonCount].vertices[verticesCount].y = Convert.ToSingle(verticesSplit[1]);
                    obstacles[obstaclesCount].polygon[polygonCount].vertices[verticesCount].z = objDepth;
                    //print("(" + obstacles[obstaclesCount].polygon[polygonCount].vertices[verticesCount].x +
                    //", " + obstacles[obstaclesCount].polygon[polygonCount].vertices[verticesCount].y + ")");
                }
            }
            // Configration
            string[] initialSplit = obstaclesDat[lineCount++].Split(' ');
            obstacles[obstaclesCount].initial.position.x = Convert.ToSingle(initialSplit[0]);
            obstacles[obstaclesCount].initial.position.y = Convert.ToSingle(initialSplit[1]);
            obstacles[obstaclesCount].initial.position.z = objDepth;
            obstacles[obstaclesCount].initial.rotation = Convert.ToSingle(initialSplit[2]);
            //print(obstacles[obstaclesCount].initial.position.x + ", " + obstacles[obstaclesCount].initial.position.y + ", " + obstacles[obstaclesCount].initial.rotation);
        }
    }

    public void drawPolygon(Vector3[] vertices, GameObject parentPoly, Material mate, string name)
    {
        // Add scale & offset
        for (int i = 0; i < vertices.GetLength(0); i++)
        {
            vertices[i] *= scale;
            vertices[i] += backgroundOffset;
        }

        // Space transform
        for (int i = 0; i < vertices.GetLength(0); i++)
            vertices[i] = mainCamera.ScreenToWorldPoint(vertices[i]);

        // UV
        Vector2[] UV = new Vector2[vertices.Length];
        for (int i = 0; i < UV.Length; i++)
        {
            UV[i] = new Vector2(vertices[i].x, vertices[i].y);
        }

        // triangles
        int[] triangles = new int[(vertices.Length - 2) * 3];
        for (int i = 0; i < (vertices.Length - 2); i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 2;
            triangles[i * 3 + 2] = i + 1;
        }

        // Create the mesh 
        Mesh polyMesh = new Mesh();
        polyMesh.vertices = vertices;
        polyMesh.uv = UV;
        polyMesh.name = "polyMesh";
        polyMesh.triangles = triangles;
        polyMesh.RecalculateNormals();
        polyMesh.RecalculateBounds();

        // Set up game object with mesh
        GameObject polyGO = new GameObject();
        polyGO.name = name;
        polyGO.AddComponent<MeshFilter>();
        polyGO.GetComponent<MeshFilter>().mesh = polyMesh;
        polyGO.AddComponent<MeshRenderer>();
        polyGO.GetComponent<MeshRenderer>().material = mate;
        polyGO.AddComponent<MeshCollider>();
        polyGO.GetComponent<MeshCollider>().sharedMesh = polyMesh;
        polyGO.transform.parent = parentPoly.transform;
    }

    /*void writeBackInitial()
    {
        for (int rob = 0; rob < robot.GetLength(0); rob++)
        {
            for (int poly = 0; poly < robot[rob].numberOfPolygon; poly++)
            {
                GameObject GO = GameObject.Find("robot" + rob + "polygon" + poly);
                Mesh polyMesh = GO.GetComponent<MeshFilter>().mesh;
                Vector3[] vertices = new Vector3[polyMesh.vertices.GetLength(0)];
                for (int ver = 0; ver < polyMesh.vertices.GetLength(0); ver++)
                    vertices[ver] = mainCamera.WorldToScreenPoint(GO.transform.TransformPoint(polyMesh.vertices[ver]));
                robot[rob].polygon[poly].vertices = vertices;
            }
        }
        for (int obs = 0; obs < obstacles.GetLength(0); obs++)
        {
            for (int poly = 0; poly < obstacles[obs].numberOfPolygon; poly++)
            {
                GameObject GO = GameObject.Find("obstacle" + obs + "polygon" + poly);
                Mesh polyMesh = GO.GetComponent<MeshFilter>().mesh;
                Vector3[] vertices = new Vector3[polyMesh.vertices.GetLength(0)];
                for (int ver = 0; ver < polyMesh.vertices.GetLength(0); ver++)
                    vertices[ver] = mainCamera.WorldToScreenPoint(GO.transform.TransformPoint(polyMesh.vertices[ver]));
                obstacles[obs].polygon[poly].vertices = vertices;
            }
        }
    }*/
    //------------------------Calculate------------------------
    public void calculate()
    {
        stopWatch.Reset();
        stopWatch.Start();  // Start timing

        initializeBitmap();
        drawObstacles();
        for (int cp = 0; cp < 2; cp++)
            NFOne(cp);

        stopWatch.Stop();   // End timing
        TimeSpan ts = stopWatch.Elapsed;
        string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
        print("Elapsed time: " + elapsedTime);
        calculateTime.text = elapsedTime;
    }

    void initializeBitmap()
    {
        for (int y = 0; y < pfTexture[0].height; y++)
            for (int x = 0; x < pfTexture[0].width; x++)
                pfTexture[0].SetPixel(x, y, new Color(254.0f / 255.0f, 254.0f / 255.0f, 254.0f / 255.0f));
        pfTexture[0].Apply();
        for (int y = 0; y < pfTexture[1].height; y++)
            for (int x = 0; x < pfTexture[1].width; x++)
                pfTexture[1].SetPixel(x, y, new Color(254.0f / 255.0f, 254.0f / 255.0f, 254.0f / 255.0f));
        pfTexture[1].Apply();
    }

    void drawObstacles()
    {
        for (int y = (int)backgroundOffset.y; y < (int)backgroundOffset.y + backgroundSize * scale; y++)
            for (int x = (int)backgroundOffset.x; x < (int)backgroundOffset.x + backgroundSize * scale; x++)
            {
                RaycastHit hitInfo = new RaycastHit();
                bool hit = Physics.Raycast(mainCamera.ScreenPointToRay(new Vector3(x, y, 0)), out hitInfo);
                if (hit)
                {
                    if (hitInfo.transform.parent.gameObject.name.Substring(0, 3) == "obs")
                    {
                        int xTex = (int)(x - backgroundOffset.x) / scale;
                        int yTex = (int)(y - backgroundOffset.y) / scale;
                        //pfTexture[0].SetPixel(xTex, yTex, Color.black);
                        pfTexture[0].SetPixel(xTex, yTex, new Color(255.0f / 255.0f, 255.0f / 255.0f, 255.0f / 255.0f));
                        pfTexture[0].Apply();
                        //pfTexture[1].SetPixel(xTex, yTex, Color.black);
                        pfTexture[1].SetPixel(xTex, yTex, new Color(255.0f / 255.0f, 255.0f / 255.0f, 255.0f / 255.0f));
                        pfTexture[1].Apply();
                    }
                }
            }
    }

    void NFOne(int cp)
    {
        int[,] U = new int[backgroundSize, backgroundSize];
        List<List<Vector2>> list = new List<List<Vector2>>();

        // Initialize U
        for (int y = 0; y < backgroundSize; y++)
            for (int x = 0; x < backgroundSize; x++)
            {
                // Assign obstacles to U from pftexture
                if (pfTexture[cp].GetPixel(x, y) == new Color(255.0f / 255.0f, 255.0f / 255.0f, 255.0f / 255.0f))
                {
                    U[x, y] = 255;
                }
                // Set free space in U to Int32.MaxValue
                if (pfTexture[cp].GetPixel(x, y) == new Color(254.0f / 255.0f, 254.0f / 255.0f, 254.0f / 255.0f))
                {
                    U[x, y] = Int32.MaxValue;
                }
            }

        // Insert goal in list
        List<Vector2> listQ = new List<Vector2>();
        for (int rob = 0; rob < robot.GetLength(0); rob++)
        {
            U[(int)(robot[rob].goal.position.x + robot[rob].controlPoint[cp].x), (int)(robot[rob].goal.position.y + robot[rob].controlPoint[cp].y)] = 0;
            listQ.Add(new Vector2(robot[rob].goal.position.x + robot[rob].controlPoint[cp].x, robot[rob].goal.position.y + robot[rob].controlPoint[cp].y));
        }
        list.Add(listQ);

        // Calculate on U
        int i = 0;
        while (list[i].Count != 0)
        {
            List<Vector2> listQtemp = new List<Vector2>();
            for (int qi = 0; qi < list[i].Count; qi++)
            {
                if ((list[i][qi].x + 1) < backgroundSize)
                    if (U[(int)(list[i][qi].x + 1), (int)list[i][qi].y] == Int32.MaxValue)  // right neighbor
                    {
                        U[(int)(list[i][qi].x + 1), (int)list[i][qi].y] = i + 1;
                        listQtemp.Add(new Vector2((list[i][qi].x + 1), list[i][qi].y));
                    }
                if ((list[i][qi].y + 1) < backgroundSize)
                    if (U[(int)list[i][qi].x, (int)(list[i][qi].y + 1)] == Int32.MaxValue)  // up neighbor
                    {
                        U[(int)list[i][qi].x, (int)(list[i][qi].y + 1)] = i + 1;
                        listQtemp.Add(new Vector2(list[i][qi].x, (list[i][qi].y + 1)));
                    }
                if ((list[i][qi].x - 1) >= 0)
                    if (U[(int)(list[i][qi].x - 1), (int)list[i][qi].y] == Int32.MaxValue)  // left neighbor
                    {
                        U[(int)(list[i][qi].x - 1), (int)list[i][qi].y] = i + 1;
                        listQtemp.Add(new Vector2((list[i][qi].x - 1), list[i][qi].y));
                    }
                if ((list[i][qi].y - 1) >= 0)
                    if (U[(int)list[i][qi].x, (int)(list[i][qi].y - 1)] == Int32.MaxValue)  // down neighbor
                    {
                        U[(int)list[i][qi].x, (int)(list[i][qi].y - 1)] = i + 1;
                        listQtemp.Add(new Vector2(list[i][qi].x, (list[i][qi].y - 1)));
                    }
            }
            list.Add(listQtemp);
            i++;
        }

        // Assign U back to pfTexture
        for (int y = 0; y < backgroundSize; y++)
            for (int x = 0; x < backgroundSize; x++)
            {
                float color = (float)U[x, y] / i;
                pfTexture[cp].SetPixel(x, y, new Color(color, color, color));
                pfTexture[cp].Apply();
            }
    }

    void scanLine(Vector3 startPoint, Vector3 goalPoint)
    {
        int x;
        float dx, dy, y, m;

        dy = goalPoint.y - startPoint.y;
        dx = goalPoint.x - startPoint.x;
        m = dy / dx;
        y = startPoint.y;
        for (x = (int)startPoint.x; x <= goalPoint.x; x++)
        {
            pfTexture[0].SetPixel(x, Mathf.RoundToInt(y), Color.black);
            y += m;
        }
        pfTexture[0].Apply();
    }

    //------------------------Button------------------------
    public void startMoving()
    {

    }

    public void backgroundTex()   // backgroundTexture
    {
        GameObject.Find("Background").GetComponent<MeshRenderer>().material.mainTexture = backgroundTexture;
    }

    public void pathTex()   // switch backgroundTexture & pathTexture
    {
        if (GameObject.Find("Background").GetComponent<MeshRenderer>().material.mainTexture == pathTexture)
            GameObject.Find("Background").GetComponent<MeshRenderer>().material.mainTexture = backgroundTexture;
        else
            GameObject.Find("Background").GetComponent<MeshRenderer>().material.mainTexture = pathTexture;
    }

    public void pfOne()   // pfTexture[0]
    {
        GameObject.Find("Background").GetComponent<MeshRenderer>().material.mainTexture = pfTexture[0];
    }

    public void pfTwo()   // pfTexture[1]
    {
        GameObject.Find("Background").GetComponent<MeshRenderer>().material.mainTexture = pfTexture[1];
    }
    //------------------------end of code------------------------
    void myBug()
    {
        for (int i = 0; i < robot[0].polygon[0].vertices.GetLength(0); i++)
        {
            print("robot[0].polygon[0].vertices[i]: " + robot[0].polygon[0].vertices[i]);
        }
        print("robot[0].initial.position: " + robot[0].initial.position);
        print("robot[0].initial.rotation: " + robot[0].initial.rotation);
    }
}
