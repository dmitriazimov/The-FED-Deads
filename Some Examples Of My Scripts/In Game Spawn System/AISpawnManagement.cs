using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#region Intro

/* The spawn system can:
 * 1. Analyse the surroundings of each spawn point and relocate it to a better position to avoid monsters instantiating outside of the map and clipping into walls
 * 2. Decide which spawn points should be active depending on how far the point is from the player and whether or not the player sees it
 * 3. Decide which monster to spawn depending on a ratio set by the devs and the type of monsters actively present in the scene
 * 4. Add mesh colliders to objects that should have them but don't i.e. walls, ceilings, pipes.
 * 5. Remove unwanted colliders
 * 6. Assign randomised waypoints for the AI to follow
 * 7. Reposition AI waypoints to avoid AI trying to go outside the navigation mesh
 */

#endregion

class AISpawnManagement : MonoBehaviour
{
    #region GameObject lists
    List<GameObject> spawnPointList;
    List<GameObject> activeSpawnPointList;
    List<GameObject> wayPoints;
    #endregion

    #region Runtime Spawn Parameters
    float floaterToWalkerRatio;
    const float desiredFloaterToWalkerRatio = 0.5f;
    float numberOfWalkers;
    float numberOfFloaters;
    const float spawnRate = 1.5f;
    float nextSpawn;
    [SerializeField] float minDistanceFromPlayer = 10f;
    [SerializeField] float maxDistanceFromPlayer = 50f;
    [SerializeField] float maxCreatures;
    #endregion

    #region SpawnPoint Reposition
    const float genericCapsuleHalfHight = 1f;
    const float genericCapsuleRadius = 0.5f;
    const float maxCastLenght = 500f;
    const float maxRaycastDistanceForWaypoints = 10f;
    const float waypointOffsetFromGround = 10f;
    const int waypointsPerCreature = 5;
    #endregion

    #region Other
    [SerializeField] GameObject walker;
    [SerializeField] GameObject floater;
    Camera playerCamera;
    enum MonsterSpawnMode { FLOATER, WALKER };
    MonsterSpawnMode currentMonsterToSpawn;
    #endregion


    void Awake()
    {
        InitElements();
    }

    void Update()
    {
        UpdateActiveAINumbers();
        DecideWhichSpawnPointsShouldBeActive();
        SpawnMonsters();
    }

    void InitElements()
    {
        InitWayPoints();
        InitSpawnPointList();
        AddTempMeshColliders();
        playerCamera = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
        currentMonsterToSpawn = new MonsterSpawnMode();
        nextSpawn = 0f;
    }
    
    void InitSpawnPointList()
    {
        activeSpawnPointList = new List<GameObject>();
        spawnPointList = new List<GameObject>();
        if (spawnPointList.Count == 0)
        {
            foreach (GameObject spawnPoint in GameObject.FindGameObjectsWithTag("FloaterAndWalkerSpawnPoint")) // Finding all the spawn points
            {
                spawnPoint.transform.position = RepositionSpawnPoint(spawnPoint); // Repositioning them to avoid instantiating outside of the map and clipping into walls
                spawnPointList.Add(spawnPoint); // Adding them to the list
            }
        }
    }

    void InitWayPoints()
    {
        wayPoints = new List<GameObject>();
        if (wayPoints.Count == 0)
        {
            foreach (GameObject wayPoint in GameObject.FindGameObjectsWithTag("WayPoint")) // Finding all way points
            {
                wayPoint.transform.position = RepositionWayPoint(wayPoint); // Repositioning them to avoid monsters going places outside the navigation mesh
                wayPoints.Add(wayPoint); // Adding them to the list
            }
        }
    }

    void UpdateActiveAINumbers()
    {
        if (GameObject.FindGameObjectsWithTag("Floater") != null)
        {
            numberOfFloaters = GameObject.FindGameObjectsWithTag("Floater").Length;
        }
        if (GameObject.FindGameObjectsWithTag("Runner") != null)
        {
            numberOfWalkers = GameObject.FindGameObjectsWithTag("Runner").Length;
        }
        floaterToWalkerRatio = numberOfFloaters / numberOfWalkers;
    }

    void DecideWhichSpawnPointsShouldBeActive()
    {
        activeSpawnPointList.Clear(); // Fresh list at each frame
        float nbOfActivePointsToBe = DecideHowManySpawnPointsShouldBeActive();
        if (nbOfActivePointsToBe > 0)
        {
            SortSpawnPointsByDistanceFromPlayer();

            foreach (GameObject spawnPoint in spawnPointList)
            {
                if (!PositionIsSeenByCamera(spawnPoint) && PositionWithinAcceptableRange(spawnPoint))
                {
                    if (spawnPoint.activeSelf == false)
                    {
                        spawnPoint.SetActive(true);
                    }
                    activeSpawnPointList.Add(spawnPoint); // Saving the spawn point if the player doesn't see it and if it's in an acceptable range
                }
                else
                {
                    if (spawnPoint.activeSelf == true)
                    {
                        spawnPoint.SetActive(false); // To avoid the player seeing a monster spawn or the monster spawning too far/close
                    }
                }
            }
        }
    }

    float DecideHowManySpawnPointsShouldBeActive()
    { // Limiting the number of spawn points according to active monster type numbers
        float nbActiveSpawns = (maxCreatures - (numberOfWalkers + numberOfFloaters)) / 2 - (maxCreatures - (numberOfWalkers + numberOfFloaters)) % 2;
        return nbActiveSpawns;
    }

    void SortSpawnPointsByDistanceFromPlayer()
    { // This script was written before the player prefab was functionnal so I was using the player camera as reference
        spawnPointList = spawnPointList.OrderBy(examinedPoint => Vector3.Distance(playerCamera.transform.position, examinedPoint.transform.position)).ToList();
    }

    bool PositionIsSeenByCamera(GameObject spawnPoint)
    {
        bool isSeen = false;
        // First we check if the transform is in the camera's spectrum
        // Alternatively, we could have used the prebuilt function in unity's GeometryUtility to calculate the camera's frustrum planes:
        // Plane[] planes = GeometryUtility.CalculateFrustumPlanes(playerCamera);
        Vector3 screenPoint = playerCamera.WorldToViewportPoint(spawnPoint.transform.position);
        if (screenPoint.z > 0 && screenPoint.x > 0 && screenPoint.x < 1 && screenPoint.y > 0 && screenPoint.y < 1)
        {
            // Then we use a ray cast to see if anything is obstructing the the view
            RaycastHit hit;
            Vector3 toSpawnPoint = spawnPoint.transform.position - playerCamera.transform.position;
            Physics.Raycast(playerCamera.transform.position, toSpawnPoint, out hit);
            if (hit.transform == spawnPoint.transform)
            {
                isSeen = true;
            }
        }
        return isSeen;
        // We could have used an AABB test on the frustrum planes:
        // GeometryUtility.TestPlanesAABB(planes, collider.bounds)
        // But the spawn point is just a transform (i.e. has no planes) with no collider so that would have been wastefull 
    }

    bool PositionWithinAcceptableRange(GameObject spawnPoint)
    { // We don't want the monster to instanciate too close or too far from the player
        bool isInAcceptableRange = false;
        float distanceFromPlayer = Vector3.Distance(spawnPoint.transform.position, playerCamera.transform.position);
        if (distanceFromPlayer < maxDistanceFromPlayer && distanceFromPlayer > minDistanceFromPlayer)
        {
            isInAcceptableRange = true;
        }

        return isInAcceptableRange;
    }

    void SpawnMonsters()
    {
        if (Time.time > nextSpawn && activeSpawnPointList.Count> 0 && (numberOfFloaters + numberOfWalkers) < maxCreatures)
        { // The monsters are instanciated at a predetermined rate
            nextSpawn = Time.time + spawnRate;
            GameObject activeSpawnPoint= activeSpawnPointList[Random.Range(0, activeSpawnPointList.Count-1)];
            {
                DecideCurrentMonsterSpawnMode(); // What type of monster should be instantiated?
                if (currentMonsterToSpawn == MonsterSpawnMode.WALKER)
                {
                    GameObject creature = Instantiate(walker, activeSpawnPoint.transform.position, Quaternion.identity);
                    creature.GetComponent<AIController>().patrolWayPoints = AddRandomWaypoints(); // Giving a patrol route
                    creature.GetComponent<AIController>().patrolBirectional = Random.value > 0.5f; // Should the monster walk back and forth? Set at random for diversity
                    creature.GetComponent<AIController>().player = GameObject.FindGameObjectWithTag("Player").transform;
                }
                else if (currentMonsterToSpawn == MonsterSpawnMode.FLOATER)
                {
                    GameObject creature = Instantiate(floater, activeSpawnPoint.transform.position, Quaternion.identity);
                    creature.GetComponent<AIController>().patrolWayPoints = AddRandomWaypoints(); // Giving a patrol route
                    creature.GetComponent<AIController>().patrolBirectional = Random.value > 0.5f; // Should the monster walk back and forth? Set at random for diversity
                    creature.GetComponent<AIController>().player = GameObject.FindGameObjectWithTag("Player").transform;
                }
            }
        }
    }

    void DecideCurrentMonsterSpawnMode()
    { // Taking the desired ratio of monster types set by the devs into account
        if (floaterToWalkerRatio > desiredFloaterToWalkerRatio)
        {
            currentMonsterToSpawn = MonsterSpawnMode.WALKER;
        }
        else
        {
            currentMonsterToSpawn = MonsterSpawnMode.FLOATER;
        }
    }

    Vector3 RepositionSpawnPoint(GameObject spawnPoint)
    {
        // The repositioning test works by a series of casts to find out how far the spawn point is from it's surroundings
        Vector3 newPos = Vector3.zero;
        bool successfulCast = true;
        // Capsule are used for the horizontal casts to mimic the monster's capsule collider
        RaycastHit capsuleLeft;
        RaycastHit capsuleRight;
        RaycastHit capsuleFront;
        RaycastHit capsuleBack;
        // Spheres are used for the vertical casts to mimic the sphyrical shape of the top and bottom of the monster's collider
        RaycastHit sphereUp;
        RaycastHit sphereDown;
        // If one of the tests fail, it means the spawn point is outside of the tunnel
        // The dev would then receive an error message with the appropriate information
        Vector3 centerSphereTop = spawnPoint.transform.position + (genericCapsuleHalfHight * Vector3.up);
        Vector3 centerSphereBottom = spawnPoint.transform.position - (genericCapsuleHalfHight * Vector3.up);
        if (!Physics.CapsuleCast(centerSphereBottom, centerSphereTop, genericCapsuleRadius, -spawnPoint.transform.right, out capsuleLeft, maxCastLenght))
        {
            Debug.LogError("Left capsule cast of " + spawnPoint.name + " unsuccessful. Make sure it is within the sewer.");
            successfulCast = false;
        }
        if (!Physics.CapsuleCast(centerSphereBottom, centerSphereTop, genericCapsuleRadius, spawnPoint.transform.right, out capsuleRight, maxCastLenght))
        {
            Debug.LogError("Right capsule cast of " + spawnPoint.name + " unsuccessful. Make sure it is within the sewer.");
            successfulCast = false;
        }
        if (!Physics.CapsuleCast(centerSphereBottom, centerSphereTop, genericCapsuleRadius, spawnPoint.transform.forward, out capsuleFront, maxCastLenght))
        {
            Debug.LogError("Front capsule cast of " + spawnPoint.name + " unsuccessful. Make sure it is within the sewer.");
            successfulCast = false;
        }
        if (!Physics.CapsuleCast(centerSphereBottom, centerSphereTop, genericCapsuleRadius, -spawnPoint.transform.forward, out capsuleBack, maxCastLenght))
        {
            Debug.LogError("Back capsule cast of " + spawnPoint.name + " unsuccessful. Make sure it is within the sewer.");
            successfulCast = false;
        }
        if (!Physics.SphereCast(spawnPoint.transform.position, genericCapsuleRadius, spawnPoint.transform.up, out sphereUp, maxCastLenght))
        {
            Debug.LogError("Up sphere cast of " + spawnPoint.name + " unsuccessful. Make sure it is within the sewer.");
            successfulCast = false;
        }
        if (!Physics.SphereCast(spawnPoint.transform.position, genericCapsuleRadius, -spawnPoint.transform.up, out sphereDown, maxCastLenght))
        {
            Debug.LogError("Bottom sphere cast of " + spawnPoint.name + " unsuccessful. Make sure it is within the sewer.");
            successfulCast = false;
        }

        if (successfulCast)
        { // If the spawn point is in the tunnel, it will be repositioned to an average distance from it's surroundings to a logical spawn
            newPos += Vector3.Lerp(capsuleLeft.point, capsuleRight.point, 0.5f);
            newPos += Vector3.Lerp(capsuleFront.point, capsuleBack.point, 0.5f);
            newPos += Vector3.Lerp(sphereDown.point, sphereUp.point, 0.5f);
            newPos /= 3;
        }

        return newPos;
    }

    void AddTempMeshColliders()
    { // Certain objects did not have colliders even though they should so I added them here
        GameObject[] gameObjects = FindObjectsOfType<GameObject>();

        foreach (GameObject piece in gameObjects)
        {
            if ((piece.name.Contains("roof") || piece.name.Contains("pipes") || piece.name.Contains("floor") 
                || piece.name.Contains("wall")) && piece.GetComponent<MeshCollider>() == null)
            {
                piece.gameObject.AddComponent<MeshCollider>();
            }
        }
    }   
    

    Vector3 RepositionWayPoint(GameObject wayPoint)
    {
        // The waypoint had to be slightly above the ground to go well with the navigation mesh
        // The repositioning consists of finding our how far the waypoint is from the ceiling and ground,
        // then adjusting it's height according to a predetermined offset
        RaycastHit ceilingHit;
        RaycastHit floorHit;
        Physics.Raycast(wayPoint.transform.position, wayPoint.transform.up, out ceilingHit, maxRaycastDistanceForWaypoints);
        Physics.Raycast(wayPoint.transform.position, -wayPoint.transform.up, out floorHit, maxRaycastDistanceForWaypoints);
        float distance = Vector3.Distance(floorHit.point, ceilingHit.point);
        distance /= waypointOffsetFromGround;
        return new Vector3(wayPoint.transform.position.x, floorHit.point.y + distance, wayPoint.transform.position.z);
    }
    
    List<Transform> AddRandomWaypoints()
    { // The AI navigation paths are randomised for a diverse experience
        List<Transform> path = new List<Transform>();
        for(int i = 0; i < waypointsPerCreature; i++)
        {
            int randomIndex = Random.Range(0, wayPoints.Count - 1);
            if (!path.Contains(wayPoints[randomIndex].transform))
            {
                path.Add(wayPoints[randomIndex].transform);
            }
        }
        return path;
    }
}
