// Planning Monster: a motion planning project of GRA class
// Weng, Wei-Chen 2017/11/15
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
    Vector3 rotateStartVectorTemp;
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

        // Read data
        readDat(Application.streamingAssetsPath + "/robot.dat", robotDat);
        readDat(Application.streamingAssetsPath + "/obstacles.dat", obstaclesDat);

        // Create background, robots & obstacles data structure
        bgCreate();
        robotCreate();
        obstaclesCreate();

        // Add scale & offset
        for (int rob = 0; rob < robot.GetLength(0); rob++)  // robot
        {
            for (int poly = 0; poly < robot[rob].numberOfPolygon; poly++)
            {
                for (int i = 0; i < robot[rob].polygon[poly].vertices.GetLength(0); i++)
                {
                    robot[rob].polygon[poly].vertices[i] *= scale;
                    robot[rob].polygon[poly].vertices[i] += backgroundOffset;
                }
            }
        }
        for (int obs = 0; obs < obstacles.GetLength(0); obs++)  // obstacle
        {
            for (int poly = 0; poly < obstacles[obs].numberOfPolygon; poly++)
            {
                for (int i = 0; i < obstacles[obs].polygon[poly].vertices.GetLength(0); i++)
                {
                    obstacles[obs].polygon[poly].vertices[i] *= scale;
                    obstacles[obs].polygon[poly].vertices[i] += backgroundOffset;
                }
            }
        }

        // Draw robots & obstacles
        for (int rob = 0; rob < robot.GetLength(0); rob++)  // robot
        {
            GameObject parentRobot = new GameObject();
            parentRobot.name = "robot" + rob;
            deleteList.Add(parentRobot.name);
            parentRobot.transform.position = mainCamera.ScreenToWorldPoint(robot[rob].initial.position * scale + backgroundOffset);
            for (int poly = 0; poly < robot[rob].numberOfPolygon; poly++)
            {
                drawPolygon(robot[rob].polygon[poly].vertices, parentRobot, robotMaterial, parentRobot.name + "polygon" + poly);
            }
            parentRobot.transform.Rotate(new Vector3(0, 0, 1), robot[rob].initial.rotation, Space.Self);
        }
        for (int obs = 0; obs < obstacles.GetLength(0); obs++)  // obstacle
        {
            GameObject parentObstacle = new GameObject();
            parentObstacle.name = "obstacle" + obs;
            deleteList.Add(parentObstacle.name);
            parentObstacle.transform.position = mainCamera.ScreenToWorldPoint(obstacles[obs].initial.position * scale + backgroundOffset);
            for (int poly = 0; poly < obstacles[obs].numberOfPolygon; poly++)
            {
                drawPolygon(obstacles[obs].polygon[poly].vertices, parentObstacle, obsMaterial, parentObstacle.name + "polygon" + poly);
            }
            parentObstacle.transform.Rotate(new Vector3(0, 0, 1), obstacles[obs].initial.rotation, Space.Self);
        }

        // Write back vertices
        writeBackVertices();
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
                if (!moving) // First hit: decide start vector
                {
                    selectedPoly = GameObject.Find(hitInfo.transform.parent.gameObject.name);
                    mouseStartPosition = new Vector3(Input.mousePosition.x, Input.mousePosition.y, objDepth);
                    rotateStartVector = (mouseStartPosition - mainCamera.WorldToScreenPoint(selectedPoly.transform.position)).normalized;
                    rotateStartVectorTemp = rotateStartVector;

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
                rotateCurrVector = (mouseCurrPosition - mainCamera.WorldToScreenPoint(selectedPoly.transform.position)).normalized;

                float rotateAngle = Vector3.Angle(rotateStartVectorTemp, rotateCurrVector);
                Vector3 getDirect = Vector3.Cross(rotateStartVectorTemp, rotateCurrVector).normalized;
                if (getDirect.z < 0)
                    rotateAngle = -rotateAngle;
                selectedPoly.transform.Rotate(new Vector3(0, 0, 1), rotateAngle, Space.Self);
                rotateStartVectorTemp = rotateCurrVector;
            }
        }
        if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1))
        {
            if (moving)
            {
                writeBackVertices();
            }

            moving = false;
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
        // Load initial configuration
        for (int rob = 0; rob < robot.GetLength(0); rob++)
        {
            for (int poly = 0; poly < robot[rob].numberOfPolygon; poly++)
            {
                for (int ver = 0; ver < robot[rob].polygon[poly].numberOfVertices; ver++)
                {
                    robot[rob].polygon[poly].vertices[ver] += robot[rob].initial.position;
                }
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
        // Load initial configuration
        for (int obs = 0; obs < obstacles.GetLength(0); obs++)
        {
            for (int poly = 0; poly < obstacles[obs].numberOfPolygon; poly++)
            {
                for (int ver = 0; ver < obstacles[obs].polygon[poly].numberOfVertices; ver++)
                {
                    obstacles[obs].polygon[poly].vertices[ver] += obstacles[obs].initial.position;
                }
            }
        }
    }

    public void drawPolygon(Vector3[] verticesSource, GameObject parentPoly, Material mate, string name)
    {
        Vector3[] vertices = new Vector3[verticesSource.GetLength(0)];
        for (int i = 0; i < vertices.GetLength(0); i++)
            vertices[i] = mainCamera.ScreenToWorldPoint(verticesSource[i]);

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

    void writeBackVertices()
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
        //for (int ver = 0; ver < robot[0].polygon[0].vertices.GetLength(0); ver++)
        //    print(robot[0].polygon[0].vertices[ver]);
    }
    //------------------------Calculate------------------------
    public void calculate()
    {
        stopWatch.Reset();
        stopWatch.Start();  // Start timing

        Vector3 startPoint = new Vector3(0, 0, objDepth);
        Vector3 goalPoint = new Vector3(backgroundSize - 1, backgroundSize - 1, objDepth);

        initializeBitmap();
        drawObstacles();
        NFOne();
        //scanLine(startPoint, goalPoint);

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
                        pfTexture[0].SetPixel(xTex, yTex, Color.black);
                        //pfTexture[0].SetPixel(xTex, yTex, new Color(255.0f/255.0f, 255.0f/255.0f, 255.0f/255.0f));
                        pfTexture[0].Apply();
                        pfTexture[1].SetPixel(xTex, yTex, Color.black);
                        //pfTexture[1].SetPixel(xTex, yTex, new Color(255.0f/255.0f, 255.0f/255.0f, 255.0f/255.0f));
                        pfTexture[1].Apply();
                    }
                }
            }
    }

    void NFOne()
    {
        int[,] calculateArray = new int[backgroundSize, backgroundSize];

        for (int y = 0; y < backgroundSize; y++)
            for (int x = 0; x < backgroundSize; x++)
            {
                if (pfTexture[0].GetPixel(x, y) == new Color(254.0f / 255.0f, 254.0f / 255.0f, 254.0f / 255.0f))
                {
                    calculateArray[x, y] = Int32.MaxValue;
                }
            }

        for (int rob = 0; rob < robot.GetLength(0); rob++)
            calculateArray[(int)robot[rob].goal.position.x, (int)robot[rob].goal.position.y] = 0;

        while()
        {

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
    public void dropdownData()
    {
        // Remove polygon & background
        for (int i = 0; i < deleteList.Count; i++)
            Destroy(GameObject.Find(deleteList[i]));
        Destroy(GameObject.Find("Background"));
        deleteList.Clear();

        // Read data
        readDat(Application.streamingAssetsPath + "/robot.dat", robotDat);
        readDat(Application.streamingAssetsPath + "/obstacles.dat", obstaclesDat);

        // Create background, robots & obstacles data structure
        bgCreate();
        robotCreate();
        obstaclesCreate();

        // Add scale & offset
        for (int rob = 0; rob < robot.GetLength(0); rob++)  // robot 
        {
            for (int poly = 0; poly < robot[rob].numberOfPolygon; poly++)
            {
                for (int i = 0; i < robot[rob].polygon[poly].vertices.GetLength(0); i++)
                {
                    robot[rob].polygon[poly].vertices[i] *= scale;
                    robot[rob].polygon[poly].vertices[i] += backgroundOffset;
                }
            }
        }
        for (int obs = 0; obs < obstacles.GetLength(0); obs++)  // obstacle
        {
            for (int poly = 0; poly < obstacles[obs].numberOfPolygon; poly++)
            {
                for (int i = 0; i < obstacles[obs].polygon[poly].vertices.GetLength(0); i++)
                {
                    obstacles[obs].polygon[poly].vertices[i] *= scale;
                    obstacles[obs].polygon[poly].vertices[i] += backgroundOffset;
                }
            }
        }

        // Draw robots & obstacles
        for (int rob = 0; rob < robot.GetLength(0); rob++)  // robot
        {
            GameObject parentRobot = new GameObject();
            parentRobot.name = "robot" + rob;
            deleteList.Add(parentRobot.name);
            parentRobot.transform.position = mainCamera.ScreenToWorldPoint(robot[rob].polygon[0].vertices[0]);
            for (int poly = 0; poly < robot[rob].numberOfPolygon; poly++)
            {
                drawPolygon(robot[rob].polygon[poly].vertices, parentRobot, robotMaterial, parentRobot.name + "polygon" + poly);
            }
        }
        for (int obs = 0; obs < obstacles.GetLength(0); obs++)  // obstacle
        {
            GameObject parentObstacle = new GameObject();
            parentObstacle.name = "obstacle" + obs;
            deleteList.Add(parentObstacle.name);
            parentObstacle.transform.position = mainCamera.ScreenToWorldPoint(obstacles[obs].polygon[0].vertices[0]);
            for (int poly = 0; poly < obstacles[obs].numberOfPolygon; poly++)
            {
                drawPolygon(obstacles[obs].polygon[poly].vertices, parentObstacle, obsMaterial, parentObstacle.name + "polygon" + poly);
            }
        }
    }

    public void mainTex()   // backgroundTexture
    {
        GameObject.Find("Background").GetComponent<MeshRenderer>().material.mainTexture = backgroundTexture;
    }

    public void pathTex()   // pathTexture
    {
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
