using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public class TrailGenerate : MonoBehaviour
    {
        public Camera Camera;
        public GameObject TrailPrefab;
        public int GenerateCount;
        //public float intervalTime = 10f;
        public float ClipPlane;
        public float Speed = 10f;
        public Vector3[] PosArray;
        private List<GameObject> TrailList;

        private void Awake()
        {
            this.PosArray = new Vector3[2];
            this.PosArray[0] = Camera.ScreenToWorldPoint(new Vector3(0, 0, ClipPlane));
            this.PosArray[1] = Camera.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, ClipPlane));




            this.TrailList = new List<GameObject>();
        }

        private void Start()
        {
            for (int i = 0; i < GenerateCount; i++)
            {
                GameObject obj = GameObject.Instantiate(this.TrailPrefab, new Vector3(Random.Range(this.PosArray[0].x, this.PosArray[1].x), Random.Range(this.PosArray[0].y, this.PosArray[1].y), 0), Quaternion.identity);
                obj.transform.SetParent(this.transform);
                obj.SetActive(true);
                this.TrailList.Add(obj);
            }
            //StartCoroutine(this.WaitThenChangePos(intervalTime));
        }

        private void Update()
        {
            foreach (var item in this.TrailList)
            {
                Vector3 dir = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0).normalized;
                item.transform.Translate(dir * Random.Range(1, 10) * this.Speed * Time.deltaTime);
            }
        }

        //private IEnumerator WaitThenChangePos(float nIntervalTime)
        //{
        //    while (true)
        //    {
        //        yield return new WaitForSeconds(nIntervalTime);
        //        foreach (var item in this.TrailList)
        //        {
        //            item.transform.localPosition = this.PosArray[Random.Range(0, 3)];
        //        }
        //    }
        //}
    }
}