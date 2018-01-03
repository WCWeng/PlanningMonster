// Planning Monster: a motion planning project of GRA class
// Weng, Wei-Chen 2018/01/02
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System;
using UnityEngine;
using UnityEngine.UI;

public class PlanningMonster : MonoBehaviour
{
    //-------------public-------------
    public Camera mainCamera;
    public Material robotMaterial;
    public Material goalMaterial;
    public Material obsMaterial;
    public Material bgMaterial;
    public Text status;
    public Text calculateTime;
    public Text startButtonText;

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
    List<int[,]> potentialField = new List<int[,]>();
    List<Vector3> pathList = new List<Vector3>();
    int NF1listMAX;
    GameObject selectedPoly;
    bool moving = false;
    Color lerpedColor;
    bool SUCCESS = false;
    Vector3 mouseStartPosition;
    Vector3 mouseCurrPosition;
    Vector3 polyStartPosition;
    Vector3 backgroundOffset;
    Vector3 moveOffset;
    Vector3 rotateStartVector;
    Vector3 rotateCurrVector;
    bool calculating = false;
    bool calculateFinished = false;

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
    public class Node
    {
        public Vector3 pointer;
        public Vector3 position;
        public Node() { }
        public Node(Vector3 ptr, Vector3 pos)
        {
            pointer = ptr;
            position = pos;
        }
    }

    // Use this for initialization
    void Start()
    {
        // Initialization
        backgroundOffset = new Vector3((float)Screen.width / 7, ((float)Screen.height - backgroundSize * scale) / 2, 0f);
        backgroundTexture = new Texture2D(backgroundSize, backgroundSize);
        pathTexture = new Texture2D(backgroundSize, backgroundSize);
        for (int i = 0; i < pfTexture.GetLength(0); i++)
            pfTexture[i] = new Texture2D(backgroundSize, backgroundSize);
        for (int x = 0; x < backgroundSize; x++)
            for (int y = 0; y < backgroundSize; y++)
            {
                backgroundTexture.SetPixel(x, y, new Color(0, 0, 0));
                pathTexture.SetPixel(x, y, new Color(0, 0, 0));
            }
        backgroundTexture.Apply();
        pathTexture.Apply();

        // Print configuration
        print("Screen width: " + Screen.width + ", height: " + Screen.height);
        print("Background Size: " + backgroundSize);

        // Load data
        loadData(0);
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
            }
        }
        if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1))
        {
            //  Write back position
            if (moving)
            {
                string[] selected = selectedPoly.name.Split(' ');
                Vector3 newPosition = (mainCamera.WorldToScreenPoint(selectedPoly.transform.position) - backgroundOffset) / scale;

                if (selected[0] == "robot")
                {
                    robot[Int32.Parse(selected[1])].initial.position.x = (int)newPosition.x;
                    robot[Int32.Parse(selected[1])].initial.position.y = (int)newPosition.y;
                    robot[Int32.Parse(selected[1])].initial.rotation = (int)selectedPoly.transform.rotation.eulerAngles.z;  // "eulerAngles.z" always returns 0~359. No need to take the remainder(%).
                }
                if (selected[0] == "goal")
                {
                    robot[Int32.Parse(selected[1])].goal.position.x = (int)newPosition.x;
                    robot[Int32.Parse(selected[1])].goal.position.y = (int)newPosition.y;
                    robot[Int32.Parse(selected[1])].goal.rotation = (int)selectedPoly.transform.rotation.eulerAngles.z;
                }
                if (selected[0] == "obstacle")
                {
                    obstacles[Int32.Parse(selected[1])].initial.position.x = (int)newPosition.x;
                    obstacles[Int32.Parse(selected[1])].initial.position.y = (int)newPosition.y;
                    obstacles[Int32.Parse(selected[1])].initial.rotation = (int)selectedPoly.transform.rotation.eulerAngles.z;
                }
            }

            moving = false;
        }
        if (calculating)
        {
            status.text = "Calculating...";
            status.color = new Color(calculateTime.color.r, calculateTime.color.g, calculateTime.color.b, Mathf.PingPong(Time.time, 1));
            lerpedColor = Color.Lerp(new Color32(114, 207, 225, 1), new Color32(225, 225, 225, 1), Mathf.PingPong(Time.time, 1.5f) / 1.5f);
            GameObject.Find("bg").GetComponent<MeshRenderer>().material.color = lerpedColor;
        }
        if (calculateFinished)
        {
            if (SUCCESS)
            {
                status.text = "Success!";
                status.color = new Color(0, 0, 0, 1);
            }
            else
            {
                status.text = "Fail.";
                status.color = new Color(0, 0, 0, 1);
            }
            GameObject.Find("bg").GetComponent<MeshRenderer>().material.color = new Color32(114, 207, 225, 1);
            TimeSpan ts = stopWatch.Elapsed;
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            calculateTime.text = elapsedTime;
            calculateTime.color = new Color(0, 0, 0, 1);
            drawPotentialFieldTexture();
            drawPathListTexture();

            calculateFinished = false;
        }
    }

    public void loadData(int data)
    {
        // Remove polygon & background
        for (int i = 0; i < deleteList.Count; i++)
            Destroy(GameObject.Find(deleteList[i]));
        if (GameObject.Find("Background"))
            Destroy(GameObject.Find("Background"));
        deleteList.Clear();

        // Read data
        robotDat.Clear();
        obstaclesDat.Clear();
        readDat(Application.streamingAssetsPath + "/robot" + data + ".dat", robotDat);
        readDat(Application.streamingAssetsPath + "/obstacles.dat", obstaclesDat);

        // Create background, robots & obstacles data structure
        bgCreate();
        robotCreate();
        obstaclesCreate();

        // Draw robots & obstacles
        for (int rob = 0; rob < robot.GetLength(0); rob++)  // initial robot
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
        for (int rob = 0; rob < robot.GetLength(0); rob++)  // goal robot
        {
            GameObject parentRobot = new GameObject();
            parentRobot.name = "goal " + rob;
            deleteList.Add(parentRobot.name);
            parentRobot.transform.position = mainCamera.ScreenToWorldPoint(robot[rob].goal.position * scale + backgroundOffset);
            for (int poly = 0; poly < robot[rob].numberOfPolygon; poly++)
            {
                Vector3[] vertices = new Vector3[robot[rob].polygon[poly].numberOfVertices];
                // Load goal configuration
                for (int ver = 0; ver < robot[rob].polygon[poly].numberOfVertices; ver++)
                {
                    vertices[ver].x = robot[rob].polygon[poly].vertices[ver].x + robot[rob].goal.position.x;
                    vertices[ver].y = robot[rob].polygon[poly].vertices[ver].y + robot[rob].goal.position.y;
                    vertices[ver].z = robot[rob].polygon[poly].vertices[ver].z;
                }
                drawPolygon(vertices, parentRobot, goalMaterial, parentRobot.name + " polygon " + poly);
            }
            parentRobot.transform.Rotate(new Vector3(0, 0, 1), robot[rob].goal.rotation, Space.Self);
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

        robot = new Robot[numberOfRobot];
        for (int robotCount = 0; robotCount < numberOfRobot; robotCount++)
        {
            // Construct robot
            robot[robotCount] = new Robot();
            // Polygon
            robot[robotCount].numberOfPolygon = Int32.Parse(robotDat[lineCount++]);
            robot[robotCount].polygon = new Polygon[robot[robotCount].numberOfPolygon];
            for (int polygonCount = 0; polygonCount < robot[robotCount].numberOfPolygon; polygonCount++)
            {
                // Vertices
                robot[robotCount].polygon[polygonCount].numberOfVertices = Int32.Parse(robotDat[lineCount++]);
                robot[robotCount].polygon[polygonCount].vertices = new Vector3[robot[robotCount].polygon[polygonCount].numberOfVertices];
                for (int verticesCount = 0; verticesCount < robot[robotCount].polygon[polygonCount].numberOfVertices; verticesCount++)
                {
                    string[] verticesSplit = robotDat[lineCount++].Split(' ');
                    robot[robotCount].polygon[polygonCount].vertices[verticesCount].x = Convert.ToSingle(verticesSplit[0]);
                    robot[robotCount].polygon[polygonCount].vertices[verticesCount].y = Convert.ToSingle(verticesSplit[1]);
                    robot[robotCount].polygon[polygonCount].vertices[verticesCount].z = objDepth;
                }
            }
            // Configration
            string[] initialSplit = robotDat[lineCount++].Split(' ');
            robot[robotCount].initial.position.x = Convert.ToSingle(initialSplit[0]);
            robot[robotCount].initial.position.y = Convert.ToSingle(initialSplit[1]);
            robot[robotCount].initial.position.z = objDepth;
            robot[robotCount].initial.rotation = Convert.ToSingle(initialSplit[2]);
            string[] goalSplit = robotDat[lineCount++].Split(' ');
            robot[robotCount].goal.position.x = Convert.ToSingle(goalSplit[0]);
            robot[robotCount].goal.position.y = Convert.ToSingle(goalSplit[1]);
            robot[robotCount].goal.position.z = objDepth;
            robot[robotCount].goal.rotation = Convert.ToSingle(goalSplit[2]);
            // Control Point
            robot[robotCount].numberOfControlPoint = Int32.Parse(robotDat[lineCount++]);
            robot[robotCount].controlPoint = new Vector3[robot[robotCount].numberOfControlPoint];
            for (int controlPointCount = 0; controlPointCount < robot[robotCount].numberOfControlPoint; controlPointCount++)
            {
                string[] controlPointSplit = robotDat[lineCount++].Split(' ');
                robot[robotCount].controlPoint[controlPointCount].x = Convert.ToSingle(controlPointSplit[0]);
                robot[robotCount].controlPoint[controlPointCount].y = Convert.ToSingle(controlPointSplit[1]);
                robot[robotCount].controlPoint[controlPointCount].z = objDepth;
            }
        }
    }

    public void obstaclesCreate()
    {
        int lineCount = 0;
        int numberOfObstacles = Int32.Parse(obstaclesDat[lineCount++]);

        obstacles = new Obstacles[numberOfObstacles];
        for (int obstaclesCount = 0; obstaclesCount < numberOfObstacles; obstaclesCount++)
        {
            // Construct obstacles
            obstacles[obstaclesCount] = new Obstacles();
            // Polygon
            obstacles[obstaclesCount].numberOfPolygon = Int32.Parse(obstaclesDat[lineCount++]);
            obstacles[obstaclesCount].polygon = new Polygon[obstacles[obstaclesCount].numberOfPolygon];
            for (int polygonCount = 0; polygonCount < obstacles[obstaclesCount].numberOfPolygon; polygonCount++)
            {
                // Vertices
                obstacles[obstaclesCount].polygon[polygonCount].numberOfVertices = Int32.Parse(obstaclesDat[lineCount++]);
                obstacles[obstaclesCount].polygon[polygonCount].vertices = new Vector3[obstacles[obstaclesCount].polygon[polygonCount].numberOfVertices];
                for (int verticesCount = 0; verticesCount < obstacles[obstaclesCount].polygon[polygonCount].numberOfVertices; verticesCount++)
                {
                    string[] verticesSplit = obstaclesDat[lineCount++].Split(' ');
                    obstacles[obstaclesCount].polygon[polygonCount].vertices[verticesCount].x = Convert.ToSingle(verticesSplit[0]);
                    obstacles[obstaclesCount].polygon[polygonCount].vertices[verticesCount].y = Convert.ToSingle(verticesSplit[1]);
                    obstacles[obstaclesCount].polygon[polygonCount].vertices[verticesCount].z = objDepth;
                }
            }
            // Configration
            string[] initialSplit = obstaclesDat[lineCount++].Split(' ');
            obstacles[obstaclesCount].initial.position.x = Convert.ToSingle(initialSplit[0]);
            obstacles[obstaclesCount].initial.position.y = Convert.ToSingle(initialSplit[1]);
            obstacles[obstaclesCount].initial.position.z = objDepth;
            obstacles[obstaclesCount].initial.rotation = Convert.ToSingle(initialSplit[2]);
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
    //------------------------Calculate------------------------
    public void calculate()
    {
        startButtonText.text = "Start";
        potentialField.Clear();
        for (int x = 0; x < backgroundSize; x++)
            for (int y = 0; y < backgroundSize; y++)
                pathTexture.SetPixel(x, y, new Color(0, 0, 0));
        pathTexture.Apply();

        initializeBitmap();
        drawObstacles();
        pfTextureToPotentialField();

        Thread exThread = new Thread(calculateThread);
        exThread.Start();
    }

    void calculateThread()
    {
        calculating = true;
        stopWatch.Reset();
        stopWatch.Start();  // Start timing
        // NF One algo.
        for (int rob = 0; rob < robot.GetLength(0); rob++)
            for (int cp = 0; cp < 2; cp++)
                NFOne(rob, cp);
        // BFS algo.
        for (int rob = 0; rob < robot.GetLength(0); rob++)
            BFS(rob);

        stopWatch.Stop();   // End timing
        calculating = false;
        calculateFinished = true;
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
                        pfTexture[0].SetPixel(xTex, yTex, new Color(255.0f / 255.0f, 255.0f / 255.0f, 255.0f / 255.0f));
                        pfTexture[0].Apply();
                        pfTexture[1].SetPixel(xTex, yTex, new Color(255.0f / 255.0f, 255.0f / 255.0f, 255.0f / 255.0f));
                        pfTexture[1].Apply();
                    }
                }
            }
    }

    void pfTextureToPotentialField()
    {
        for (int cp = 0; cp < 2; cp++)
        {
            int[,] U = new int[backgroundSize, backgroundSize];
            for (int y = 0; y < backgroundSize; y++)
            {
                for (int x = 0; x < backgroundSize; x++)
                {
                    // Assign obstacles from pftexture to U
                    if (pfTexture[cp].GetPixel(x, y) == new Color(255.0f / 255.0f, 255.0f / 255.0f, 255.0f / 255.0f))
                    {
                        U[x, y] = 255;
                    }
                    // Set free space to Int32.MaxValue in U
                    if (pfTexture[cp].GetPixel(x, y) == new Color(254.0f / 255.0f, 254.0f / 255.0f, 254.0f / 255.0f))
                    {
                        U[x, y] = Int32.MaxValue;
                    }
                }
            }

            // Add U to "potentialField"
            potentialField.Add(U);
        }
    }

    void drawPotentialFieldTexture()
    {
        // Draw potentialField[cp] on pfTexture
        for (int cp = 0; cp < 2; cp++)
        {
            for (int y = 0; y < backgroundSize; y++)
            {
                for (int x = 0; x < backgroundSize; x++)
                {
                    float color = (float)potentialField[cp][x, y] / NF1listMAX;
                    pfTexture[cp].SetPixel(x, y, new Color(color, color, color));
                }
            }
            pfTexture[cp].Apply();
        }
    }

    void drawPathListTexture()
    {
        // Draw path
        for (int i = 0; i < pathList.Count - 1; i++)
        {
            pathTexture.SetPixel((int)pathList[i].x, (int)pathList[i].y, new Color(1, 0.55f, 0));
        }
        pathTexture.Apply();
    }
    //------------------------Algorithm------------------------
    void NFOne(int rob, int cp)
    {
        List<List<Vector2>> list = new List<List<Vector2>>();

        // Insert goal in list
        List<Vector2> listQ = new List<Vector2>();
        float xRotatedCP, yRotatedCP;   // Rotated control points
        xRotatedCP = robot[rob].controlPoint[cp].x * (float)Math.Cos(robot[rob].goal.rotation * (Math.PI / 180.0)) - robot[rob].controlPoint[cp].y * (float)Math.Sin(robot[rob].goal.rotation * (Math.PI / 180.0));
        yRotatedCP = robot[rob].controlPoint[cp].x * (float)Math.Sin(robot[rob].goal.rotation * (Math.PI / 180.0)) + robot[rob].controlPoint[cp].y * (float)Math.Cos(robot[rob].goal.rotation * (Math.PI / 180.0));
        potentialField[cp][(int)(robot[rob].goal.position.x + xRotatedCP), (int)(robot[rob].goal.position.y + yRotatedCP)] = 0;
        listQ.Add(new Vector2(robot[rob].goal.position.x + xRotatedCP, robot[rob].goal.position.y + yRotatedCP));
        list.Add(listQ);

        // Calculate potentialField[cp]
        int i = 0;
        while (list[i].Count != 0)
        {
            List<Vector2> listQtemp = new List<Vector2>();
            for (int qi = 0; qi < list[i].Count; qi++)
            {
                if ((list[i][qi].x + 1) < backgroundSize)
                    if (potentialField[cp][(int)(list[i][qi].x + 1), (int)list[i][qi].y] == Int32.MaxValue)  // right neighbor
                    {
                        potentialField[cp][(int)(list[i][qi].x + 1), (int)list[i][qi].y] = i + 1;
                        listQtemp.Add(new Vector2((list[i][qi].x + 1), list[i][qi].y));
                    }
                if ((list[i][qi].y + 1) < backgroundSize)
                    if (potentialField[cp][(int)list[i][qi].x, (int)(list[i][qi].y + 1)] == Int32.MaxValue)  // up neighbor
                    {
                        potentialField[cp][(int)list[i][qi].x, (int)(list[i][qi].y + 1)] = i + 1;
                        listQtemp.Add(new Vector2(list[i][qi].x, (list[i][qi].y + 1)));
                    }
                if ((list[i][qi].x - 1) >= 0)
                    if (potentialField[cp][(int)(list[i][qi].x - 1), (int)list[i][qi].y] == Int32.MaxValue)  // left neighbor
                    {
                        potentialField[cp][(int)(list[i][qi].x - 1), (int)list[i][qi].y] = i + 1;
                        listQtemp.Add(new Vector2((list[i][qi].x - 1), list[i][qi].y));
                    }
                if ((list[i][qi].y - 1) >= 0)
                    if (potentialField[cp][(int)list[i][qi].x, (int)(list[i][qi].y - 1)] == Int32.MaxValue)  // down neighbor
                    {
                        potentialField[cp][(int)list[i][qi].x, (int)(list[i][qi].y - 1)] = i + 1;
                        listQtemp.Add(new Vector2(list[i][qi].x, (list[i][qi].y - 1)));
                    }
            }
            list.Add(listQtemp);
            i++;
        }
        NF1listMAX = i;
    }

    void BFS(int rob)
    {
        List<List<Vector3>> OPEN = new List<List<Vector3>>();
        List<Node> Tree = new List<Node>();
        List<Vector3> visited = new List<Vector3>();
        int angle = 1;  // rotate angle during every step
        SUCCESS = false;
        int step = 0;

        // Initialize "OPEN" list
        for (int i = 0; i < 10000; i++)
            OPEN.Add(new List<Vector3>());

        // Insert initial position in "OPEN"
        Vector3 initNewPos = new Vector3(robot[rob].initial.position.x, robot[rob].initial.position.y, robot[rob].initial.rotation);
        Vector2[] initRotCP = new Vector2[2];  // Rotated control points
        for (int cp = 0; cp < 2; cp++)
        {
            initRotCP[cp].x = robot[rob].controlPoint[cp].x * (float)Math.Cos(initNewPos.z * (Math.PI / 180.0)) - robot[rob].controlPoint[cp].y * (float)Math.Sin(initNewPos.z * (Math.PI / 180.0));
            initRotCP[cp].y = robot[rob].controlPoint[cp].x * (float)Math.Sin(initNewPos.z * (Math.PI / 180.0)) + robot[rob].controlPoint[cp].y * (float)Math.Cos(initNewPos.z * (Math.PI / 180.0));
        }
        int initPV = Int32.MaxValue;
        if ((int)(initNewPos.x + initRotCP[0].x) < potentialField[0].GetLength(0) && (int)(initNewPos.x + initRotCP[0].x) >= 0 &&
            (int)(initNewPos.y + initRotCP[0].y) < potentialField[0].GetLength(1) && (int)(initNewPos.y + initRotCP[0].y) >= 0 &&
            (int)(initNewPos.x + initRotCP[1].x) < potentialField[1].GetLength(0) && (int)(initNewPos.x + initRotCP[1].x) >= 0 &&
            (int)(initNewPos.y + initRotCP[1].y) < potentialField[1].GetLength(1) && (int)(initNewPos.y + initRotCP[1].y) >= 0)
        {
            if (!collision(rob, initNewPos))
            {
                initPV = potentialField[0][(int)(initNewPos.x + initRotCP[0].x), (int)(initNewPos.y + initRotCP[0].y)] + potentialField[1][(int)(initNewPos.x + initRotCP[1].x), (int)(initNewPos.y + initRotCP[1].y)];
                visited.Add(initNewPos);
                Tree.Add(new Node(new Vector3(-1, -1, -1), initNewPos));
                OPEN[initPV].Add(initNewPos);
                if (initPV == 0)
                {
                    SUCCESS = true;
                }
            }
        }

        while (!EMPTY(OPEN) && !SUCCESS)
        {
            step++;

            // Storage potential value & rotated control-points of six direction.
            // 0: up, 1: bottom, 2: left, 3: right, 4: counterclockwise, 5: clockwise
            Vector3[] newPos = new Vector3[6];  // x, y, rotation
            Vector2[] rotCP = new Vector2[2];  // Rotated control points
            int[] PV = new int[6];  // sum of potential value of two control points

            // Insert FIRST(OPEN)
            Vector3 nowPosition = new Vector3();
            Vector3 temp = FIRST(OPEN);
            nowPosition.x = temp.x;
            nowPosition.y = temp.y;
            nowPosition.z = temp.z;

            // up
            newPos[0].x = nowPosition.x;
            newPos[0].y = nowPosition.y + 1;
            newPos[0].z = nowPosition.z;
            for (int cp = 0; cp < 2; cp++)
            {
                rotCP[cp].x = robot[rob].controlPoint[cp].x * (float)Math.Cos(newPos[0].z * (Math.PI / 180.0)) - robot[rob].controlPoint[cp].y * (float)Math.Sin(newPos[0].z * (Math.PI / 180.0));
                rotCP[cp].y = robot[rob].controlPoint[cp].x * (float)Math.Sin(newPos[0].z * (Math.PI / 180.0)) + robot[rob].controlPoint[cp].y * (float)Math.Cos(newPos[0].z * (Math.PI / 180.0));
            }
            if ((int)(newPos[0].x + rotCP[0].x) < potentialField[0].GetLength(0) && (int)(newPos[0].x + rotCP[0].x) >= 0 &&
                (int)(newPos[0].y + rotCP[0].y) < potentialField[0].GetLength(1) && (int)(newPos[0].y + rotCP[0].y) >= 0 &&
                (int)(newPos[0].x + rotCP[1].x) < potentialField[1].GetLength(0) && (int)(newPos[0].x + rotCP[1].x) >= 0 &&
                (int)(newPos[0].y + rotCP[1].y) < potentialField[1].GetLength(1) && (int)(newPos[0].y + rotCP[1].y) >= 0)
            {
                if (!collision(rob, newPos[0]))
                {
                    if (!visited.Exists(x => x == newPos[0]))
                    {
                        PV[0] = potentialField[0][(int)(newPos[0].x + rotCP[0].x), (int)(newPos[0].y + rotCP[0].y)] + potentialField[1][(int)(newPos[0].x + rotCP[1].x), (int)(newPos[0].y + rotCP[1].y)];
                        Tree.Add(new Node(nowPosition, newPos[0]));
                        visited.Add(newPos[0]);
                        OPEN[PV[0]].Add(newPos[0]);
                        if (PV[0] == 0)
                        {
                            SUCCESS = true;
                            break;
                        }
                    }
                }
            }
            // bottom
            newPos[1].x = nowPosition.x;
            newPos[1].y = nowPosition.y - 1;
            newPos[1].z = nowPosition.z;
            for (int cp = 0; cp < 2; cp++)
            {
                rotCP[cp].x = robot[rob].controlPoint[cp].x * (float)Math.Cos(newPos[1].z * (Math.PI / 180.0)) - robot[rob].controlPoint[cp].y * (float)Math.Sin(newPos[1].z * (Math.PI / 180.0));
                rotCP[cp].y = robot[rob].controlPoint[cp].x * (float)Math.Sin(newPos[1].z * (Math.PI / 180.0)) + robot[rob].controlPoint[cp].y * (float)Math.Cos(newPos[1].z * (Math.PI / 180.0));
            }
            if ((int)(newPos[1].x + rotCP[0].x) < potentialField[0].GetLength(0) && (int)(newPos[1].x + rotCP[0].x) >= 0 &&
                (int)(newPos[1].y + rotCP[0].y) < potentialField[0].GetLength(1) && (int)(newPos[1].y + rotCP[0].y) >= 0 &&
                (int)(newPos[1].x + rotCP[1].x) < potentialField[1].GetLength(0) && (int)(newPos[1].x + rotCP[1].x) >= 0 &&
                (int)(newPos[1].y + rotCP[1].y) < potentialField[1].GetLength(1) && (int)(newPos[1].y + rotCP[1].y) >= 0)
            {
                if (!collision(rob, newPos[1]))
                {
                    if (!visited.Exists(x => x == newPos[1]))
                    {
                        PV[1] = potentialField[0][(int)(newPos[1].x + rotCP[0].x), (int)(newPos[1].y + rotCP[0].y)] + potentialField[1][(int)(newPos[1].x + rotCP[1].x), (int)(newPos[1].y + rotCP[1].y)];
                        Tree.Add(new Node(nowPosition, newPos[1]));
                        visited.Add(newPos[1]);
                        OPEN[PV[1]].Add(newPos[1]);
                        if (PV[1] == 0)
                        {
                            SUCCESS = true;
                            break;
                        }
                    }
                }
            }
            // left
            newPos[2].x = nowPosition.x - 1;
            newPos[2].y = nowPosition.y;
            newPos[2].z = nowPosition.z;
            for (int cp = 0; cp < 2; cp++)
            {
                rotCP[cp].x = robot[rob].controlPoint[cp].x * (float)Math.Cos(newPos[2].z * (Math.PI / 180.0)) - robot[rob].controlPoint[cp].y * (float)Math.Sin(newPos[2].z * (Math.PI / 180.0));
                rotCP[cp].y = robot[rob].controlPoint[cp].x * (float)Math.Sin(newPos[2].z * (Math.PI / 180.0)) + robot[rob].controlPoint[cp].y * (float)Math.Cos(newPos[2].z * (Math.PI / 180.0));
            }
            if ((int)(newPos[2].x + rotCP[0].x) < potentialField[0].GetLength(0) && (int)(newPos[2].x + rotCP[0].x) >= 0 &&
                (int)(newPos[2].y + rotCP[0].y) < potentialField[0].GetLength(1) && (int)(newPos[2].y + rotCP[0].y) >= 0 &&
                (int)(newPos[2].x + rotCP[1].x) < potentialField[1].GetLength(0) && (int)(newPos[2].x + rotCP[1].x) >= 0 &&
                (int)(newPos[2].y + rotCP[1].y) < potentialField[1].GetLength(1) && (int)(newPos[2].y + rotCP[1].y) >= 0)
            {
                if (!collision(rob, newPos[2]))
                {
                    if (!visited.Exists(x => x == newPos[2]))
                    {
                        PV[2] = potentialField[0][(int)(newPos[2].x + rotCP[0].x), (int)(newPos[2].y + rotCP[0].y)] + potentialField[1][(int)(newPos[2].x + rotCP[1].x), (int)(newPos[2].y + rotCP[1].y)];
                        Tree.Add(new Node(nowPosition, newPos[2]));
                        visited.Add(newPos[2]);
                        OPEN[PV[2]].Add(newPos[2]);
                        if (PV[2] == 0)
                        {
                            SUCCESS = true;
                            break;
                        }
                    }
                }
            }
            // right
            newPos[3].x = nowPosition.x + 1;
            newPos[3].y = nowPosition.y;
            newPos[3].z = nowPosition.z;
            for (int cp = 0; cp < 2; cp++)
            {
                rotCP[cp].x = robot[rob].controlPoint[cp].x * (float)Math.Cos(newPos[3].z * (Math.PI / 180.0)) - robot[rob].controlPoint[cp].y * (float)Math.Sin(newPos[3].z * (Math.PI / 180.0));
                rotCP[cp].y = robot[rob].controlPoint[cp].x * (float)Math.Sin(newPos[3].z * (Math.PI / 180.0)) + robot[rob].controlPoint[cp].y * (float)Math.Cos(newPos[3].z * (Math.PI / 180.0));
            }
            if ((int)(newPos[3].x + rotCP[0].x) < potentialField[0].GetLength(0) && (int)(newPos[3].x + rotCP[0].x) >= 0 &&
                (int)(newPos[3].y + rotCP[0].y) < potentialField[0].GetLength(1) && (int)(newPos[3].y + rotCP[0].y) >= 0 &&
                (int)(newPos[3].x + rotCP[1].x) < potentialField[1].GetLength(0) && (int)(newPos[3].x + rotCP[1].x) >= 0 &&
                (int)(newPos[3].y + rotCP[1].y) < potentialField[1].GetLength(1) && (int)(newPos[3].y + rotCP[1].y) >= 0)
            {
                if (!collision(rob, newPos[3]))
                {
                    if (!visited.Exists(x => x == newPos[3]))
                    {
                        PV[3] = potentialField[0][(int)(newPos[3].x + rotCP[0].x), (int)(newPos[3].y + rotCP[0].y)] + potentialField[1][(int)(newPos[3].x + rotCP[1].x), (int)(newPos[3].y + rotCP[1].y)];
                        Tree.Add(new Node(nowPosition, newPos[3]));
                        visited.Add(newPos[3]);
                        OPEN[PV[3]].Add(newPos[3]);
                        if (PV[3] == 0)
                        {
                            SUCCESS = true;
                            break;
                        }
                    }
                }
            }
            // counterclockwise
            newPos[4].x = nowPosition.x;
            newPos[4].y = nowPosition.y;
            newPos[4].z = nowPosition.z + angle;
            if (newPos[4].z >= 360)
                newPos[4].z %= 360;
            for (int cp = 0; cp < 2; cp++)
            {
                rotCP[cp].x = robot[rob].controlPoint[cp].x * (float)Math.Cos(newPos[4].z * (Math.PI / 180.0)) - robot[rob].controlPoint[cp].y * (float)Math.Sin(newPos[4].z * (Math.PI / 180.0));
                rotCP[cp].y = robot[rob].controlPoint[cp].x * (float)Math.Sin(newPos[4].z * (Math.PI / 180.0)) + robot[rob].controlPoint[cp].y * (float)Math.Cos(newPos[4].z * (Math.PI / 180.0));
            }
            if ((int)(newPos[4].x + rotCP[0].x) < potentialField[0].GetLength(0) && (int)(newPos[4].x + rotCP[0].x) >= 0 &&
                (int)(newPos[4].y + rotCP[0].y) < potentialField[0].GetLength(1) && (int)(newPos[4].y + rotCP[0].y) >= 0 &&
                (int)(newPos[4].x + rotCP[1].x) < potentialField[1].GetLength(0) && (int)(newPos[4].x + rotCP[1].x) >= 0 &&
                (int)(newPos[4].y + rotCP[1].y) < potentialField[1].GetLength(1) && (int)(newPos[4].y + rotCP[1].y) >= 0)
            {
                if (!collision(rob, newPos[4]))
                {
                    if (!visited.Exists(x => x == newPos[4]))
                    {
                        PV[4] = potentialField[0][(int)(newPos[4].x + rotCP[0].x), (int)(newPos[4].y + rotCP[0].y)] + potentialField[1][(int)(newPos[4].x + rotCP[1].x), (int)(newPos[4].y + rotCP[1].y)];
                        Tree.Add(new Node(nowPosition, newPos[4]));
                        visited.Add(newPos[4]);
                        OPEN[PV[4]].Add(newPos[4]);
                        if (PV[4] == 0)
                        {
                            SUCCESS = true;
                            break;
                        }
                    }
                }
            }
            // clockwise
            newPos[5].x = nowPosition.x;
            newPos[5].y = nowPosition.y;
            newPos[5].z = nowPosition.z - angle;
            if (newPos[5].z < 0)
                newPos[5].z += 360; // % will still return minus angle.
            for (int cp = 0; cp < 2; cp++)
            {
                rotCP[cp].x = robot[rob].controlPoint[cp].x * (float)Math.Cos(newPos[5].z * (Math.PI / 180.0)) - robot[rob].controlPoint[cp].y * (float)Math.Sin(newPos[5].z * (Math.PI / 180.0));
                rotCP[cp].y = robot[rob].controlPoint[cp].x * (float)Math.Sin(newPos[5].z * (Math.PI / 180.0)) + robot[rob].controlPoint[cp].y * (float)Math.Cos(newPos[5].z * (Math.PI / 180.0));
            }
            if ((int)(newPos[5].x + rotCP[0].x) < potentialField[0].GetLength(0) && (int)(newPos[5].x + rotCP[0].x) >= 0 &&
                (int)(newPos[5].y + rotCP[0].y) < potentialField[0].GetLength(1) && (int)(newPos[5].y + rotCP[0].y) >= 0 &&
                (int)(newPos[5].x + rotCP[1].x) < potentialField[1].GetLength(0) && (int)(newPos[5].x + rotCP[1].x) >= 0 &&
                (int)(newPos[5].y + rotCP[1].y) < potentialField[1].GetLength(1) && (int)(newPos[5].y + rotCP[1].y) >= 0)
            {
                if (!collision(rob, newPos[5]))
                {
                    if (!visited.Exists(x => x == newPos[5]))
                    {
                        PV[5] = potentialField[0][(int)(newPos[5].x + rotCP[0].x), (int)(newPos[5].y + rotCP[0].y)] + potentialField[1][(int)(newPos[5].x + rotCP[1].x), (int)(newPos[5].y + rotCP[1].y)];
                        Tree.Add(new Node(nowPosition, newPos[5]));
                        visited.Add(newPos[5]);
                        OPEN[PV[5]].Add(newPos[5]);
                        if (PV[5] == 0)
                        {
                            SUCCESS = true;
                            break;
                        }
                    }
                }
            }
        }

        if (Tree.Count != 0)
            treeToPathList(Tree);

        if (SUCCESS)
        {
            print("Finding path successful.");
            print("Do BFS " + step + " times.");
            print("Take " + pathList.Count + " steps to goal.");
        }
        else
        {
            print("Fail to find path.");
            print("Do BFS " + step + " times.");
        }
    }

    Vector3 FIRST(List<List<Vector3>> OPEN)
    {
        Vector3 first = new Vector3();

        for (int i = 0; i < OPEN.Count; i++)
        {
            if (OPEN[i].Count != 0)
            {
                first.x = OPEN[i][OPEN[i].Count - 1].x;
                first.y = OPEN[i][OPEN[i].Count - 1].y;
                first.z = OPEN[i][OPEN[i].Count - 1].z;
                OPEN[i].Remove(first);
                break;
            }
        }
        return first;
    }

    bool EMPTY(List<List<Vector3>> OPEN)
    {
        for (int i = 0; i < OPEN.Count; i++)
        {
            if (OPEN[i].Count != 0)
            {
                return false;
            }
        }
        return true;
    }

    void treeToPathList(List<Node> Tree)
    {
        pathList.Clear();

        // Insert last node in "pathList"
        int i = Tree.Count - 1;
        pathList.Add(Tree[i].position);

        while (Tree[i].pointer != new Vector3(-1, -1, -1))
        {
            Vector3 ptr = Tree[i].pointer;
            i = Tree.FindIndex(x => x.position == ptr);
            Vector3 pos = Tree[i].position;
            pathList.Add(pos);
        }

        pathList.Reverse();
    }

    bool collision(int rob, Vector3 newPos)
    {
        // egde = (p1, p2). p1, p2 = Vector2.
        List<List<List<Vector2>>> robPolyList = new List<List<List<Vector2>>>();    // robot - polygon - edge
        List<List<List<List<Vector2>>>> obsList = new List<List<List<List<Vector2>>>>();    // obsacle - polygon - edge

        // robot list
        for (int poly = 0; poly < robot[rob].numberOfPolygon; poly++)
        {
            List<List<Vector2>> tempPoly = new List<List<Vector2>>();
            for (int ver = 0; ver < robot[rob].polygon[poly].numberOfVertices; ver++)
            {
                List<Vector2> tempEdge = new List<Vector2>();
                Vector2 p1 = new Vector2();
                Vector2 p2 = new Vector2();
                p1.x = robot[rob].polygon[poly].vertices[ver].x * (float)Math.Cos(newPos.z * (Math.PI / 180.0)) - robot[rob].polygon[poly].vertices[ver].y * (float)Math.Sin(newPos.z * (Math.PI / 180.0)) + newPos.x;
                p1.y = robot[rob].polygon[poly].vertices[ver].x * (float)Math.Sin(newPos.z * (Math.PI / 180.0)) + robot[rob].polygon[poly].vertices[ver].y * (float)Math.Cos(newPos.z * (Math.PI / 180.0)) + newPos.y;
                if ((ver + 1) == robot[rob].polygon[poly].numberOfVertices)
                {
                    p2.x = robot[rob].polygon[poly].vertices[0].x * (float)Math.Cos(newPos.z * (Math.PI / 180.0)) - robot[rob].polygon[poly].vertices[0].y * (float)Math.Sin(newPos.z * (Math.PI / 180.0)) + newPos.x;
                    p2.y = robot[rob].polygon[poly].vertices[0].x * (float)Math.Sin(newPos.z * (Math.PI / 180.0)) + robot[rob].polygon[poly].vertices[0].y * (float)Math.Cos(newPos.z * (Math.PI / 180.0)) + newPos.y;
                }
                else
                {
                    p2.x = robot[rob].polygon[poly].vertices[ver + 1].x * (float)Math.Cos(newPos.z * (Math.PI / 180.0)) - robot[rob].polygon[poly].vertices[ver + 1].y * (float)Math.Sin(newPos.z * (Math.PI / 180.0)) + newPos.x;
                    p2.y = robot[rob].polygon[poly].vertices[ver + 1].x * (float)Math.Sin(newPos.z * (Math.PI / 180.0)) + robot[rob].polygon[poly].vertices[ver + 1].y * (float)Math.Cos(newPos.z * (Math.PI / 180.0)) + newPos.y;
                }
                tempEdge.Add(p1);
                tempEdge.Add(p2);
                tempPoly.Add(tempEdge);
            }
            robPolyList.Add(tempPoly);
        }
        // obstacle list
        for (int obs = 0; obs < obstacles.GetLength(0); obs++)
        {
            List<List<List<Vector2>>> tempObs = new List<List<List<Vector2>>>();
            for (int poly = 0; poly < obstacles[obs].numberOfPolygon; poly++)
            {
                List<List<Vector2>> tempPoly = new List<List<Vector2>>();
                for (int ver = 0; ver < obstacles[obs].polygon[poly].numberOfVertices; ver++)
                {
                    List<Vector2> tempEdge = new List<Vector2>();
                    Vector2 p3 = new Vector2();
                    Vector2 p4 = new Vector2();
                    p3.x = obstacles[obs].polygon[poly].vertices[ver].x * (float)Math.Cos(obstacles[obs].initial.rotation * (Math.PI / 180.0)) - obstacles[obs].polygon[poly].vertices[ver].y * (float)Math.Sin(obstacles[obs].initial.rotation * (Math.PI / 180.0)) + obstacles[obs].initial.position.x;
                    p3.y = obstacles[obs].polygon[poly].vertices[ver].x * (float)Math.Sin(obstacles[obs].initial.rotation * (Math.PI / 180.0)) + obstacles[obs].polygon[poly].vertices[ver].y * (float)Math.Cos(obstacles[obs].initial.rotation * (Math.PI / 180.0)) + obstacles[obs].initial.position.y;
                    if ((ver + 1) == obstacles[obs].polygon[poly].numberOfVertices)
                    {
                        p4.x = obstacles[obs].polygon[poly].vertices[0].x * (float)Math.Cos(obstacles[obs].initial.rotation * (Math.PI / 180.0)) - obstacles[obs].polygon[poly].vertices[0].y * (float)Math.Sin(obstacles[obs].initial.rotation * (Math.PI / 180.0)) + obstacles[obs].initial.position.x;
                        p4.y = obstacles[obs].polygon[poly].vertices[0].x * (float)Math.Sin(obstacles[obs].initial.rotation * (Math.PI / 180.0)) + obstacles[obs].polygon[poly].vertices[0].y * (float)Math.Cos(obstacles[obs].initial.rotation * (Math.PI / 180.0)) + obstacles[obs].initial.position.y;
                    }
                    else
                    {
                        p4.x = obstacles[obs].polygon[poly].vertices[ver + 1].x * (float)Math.Cos(obstacles[obs].initial.rotation * (Math.PI / 180.0)) - obstacles[obs].polygon[poly].vertices[ver + 1].y * (float)Math.Sin(obstacles[obs].initial.rotation * (Math.PI / 180.0)) + obstacles[obs].initial.position.x;
                        p4.y = obstacles[obs].polygon[poly].vertices[ver + 1].x * (float)Math.Sin(obstacles[obs].initial.rotation * (Math.PI / 180.0)) + obstacles[obs].polygon[poly].vertices[ver + 1].y * (float)Math.Cos(obstacles[obs].initial.rotation * (Math.PI / 180.0)) + obstacles[obs].initial.position.y;
                    }
                    tempEdge.Add(p3);
                    tempEdge.Add(p4);
                    tempPoly.Add(tempEdge);
                }
                tempObs.Add(tempPoly);
            }
            obsList.Add(tempObs);
        }

        // collision detection
        for (int robPoly = 0; robPoly < robPolyList.Count; robPoly++)
        {
            for (int robEdg = 0; robEdg < robPolyList[robPoly].Count; robEdg++)
            {
                Vector2 p1 = robPolyList[robPoly][robEdg][0];
                Vector2 p2 = robPolyList[robPoly][robEdg][1];
                Vector2 v12 = p2 - p1;
                Vector2 v12p = new Vector2(v12.y, -v12.x);

                for (int obs = 0; obs < obsList.Count; obs++)
                {
                    for (int obsPoly = 0; obsPoly < obsList[obs].Count; obsPoly++)
                    {
                        for (int obsEdg = 0; obsEdg < obsList[obs][obsPoly].Count; obsEdg++)
                        {
                            Vector2 p3 = obsList[obs][obsPoly][obsEdg][0];
                            Vector2 p4 = obsList[obs][obsPoly][obsEdg][1];
                            Vector2 v13 = p3 - p1;
                            Vector2 v14 = p4 - p1;
                            float product1 = Vector2.Dot(v12p, v13) * Vector2.Dot(v12p, v14);
                            Vector2 v34 = p4 - p3;
                            Vector2 v34p = new Vector2(v34.y, -v34.x);
                            Vector2 v31 = p1 - p3;
                            Vector2 v32 = p2 - p3;
                            float product2 = Vector2.Dot(v34p, v31) * Vector2.Dot(v34p, v32);
                            if (p1.x < 0 || p1.x >= backgroundSize || p1.y < 0 || p1.y >= backgroundSize ||
                            p2.x < 0 || p2.x >= backgroundSize || p2.y < 0 || p2.y >= backgroundSize)
                                return true;
                            if (product1 < 0 && product2 < 0)
                                return true;
                        }
                    }
                }
            }
        }
        return false;
    }
    //------------------------Button------------------------
    public void startMoving()
    {
        startButtonText.text = "Replay";
        StartCoroutine(Move());
    }

    IEnumerator Move()
    {
        Vector3 newPos = new Vector3();
        float newX;
        float newY;

        for (int i = 0; i < pathList.Count; i++)
        {
            newX = pathList[i].x * scale + backgroundOffset.x;
            newY = pathList[i].y * scale + backgroundOffset.y;
            newPos = mainCamera.ScreenToWorldPoint(new Vector3(newX, newY, objDepth));
            GameObject.Find("robot 0").transform.position = newPos;
            GameObject.Find("robot 0").transform.eulerAngles = new Vector3(0, 0, pathList[i].z);
            yield return new WaitForSeconds(0.016f);
        }
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
}