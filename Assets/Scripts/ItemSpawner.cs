using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class ItemSpawner : MonoBehaviour
{
    public GameObject[] items;
    public Transform playerTransform;

    private float lastSpawnTime;
    public float maxDistance = 5f;

    private float timeBetSpawn;

    public float timeBetSpawnMax = 7f;
    public float timeBetSpawnMin = 2f;

    private void Start()
    {
        timeBetSpawn = Random.Range(timeBetSpawnMin, timeBetSpawnMax);
        lastSpawnTime = 0;

        StartCoroutine(BeginSpawn());


    }

    IEnumerator BeginSpawn()
    {
        while (true)
        {
            if (playerTransform != null)
            {
                Spawn();
                yield return new WaitForSeconds(timeBetSpawn);
                timeBetSpawn = Random.Range(timeBetSpawnMin, timeBetSpawnMax);
            }


        }

    }




    private void Update()
    {

    }

    private void Spawn()
    {
        var spawnPosition
            = Utility.GetRandomPointOnNavMesh(playerTransform.position, maxDistance, NavMesh.AllAreas);

        spawnPosition += Vector3.up * 0.5f;

        var item = Instantiate(items[Random.Range(0, items.Length)], spawnPosition, Quaternion.identity);

        Destroy(item, 5f);
    }
}