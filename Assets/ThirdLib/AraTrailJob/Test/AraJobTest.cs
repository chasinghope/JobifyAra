using UnityEngine;

public class AraJobTest : MonoBehaviour
{
    public Vector3 dir = new Vector3(1, 0, 0);
    public float speed = 20f;

    private void Awake()
    {

    }

    public void Update()
    {


        this.transform.Translate(dir.normalized * speed * Time.deltaTime, Space.Self);
    }
}