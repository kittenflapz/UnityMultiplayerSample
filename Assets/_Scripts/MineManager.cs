using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MineManager : MonoBehaviour
{
    [SerializeField]
    public List<Vector3> minePositions;

    public GameObject minePrefab;

    public List<Mine> mines;

    private void Start()
    {
        foreach(Vector3 position in minePositions) // spawn a mine at each of the specified positions
        {
            Instantiate(minePrefab, position, Quaternion.identity, this.transform);
            mines.Add(minePrefab.GetComponent<Mine>());
        }
    }
}
