using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public class TrailGenerate : MonoBehaviour
    {
        public bool UseJobify;
        public Camera Camera;
        [Header("原始")]
        public GameObject TrailPrefab;
        [Header("Job化")]
        public GameObject TrailJobPrefab;
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
                GameObject obj = GameObject.Instantiate(this.UseJobify ? this.TrailJobPrefab : this.TrailPrefab, new Vector3(Random.Range(this.PosArray[0].x, this.PosArray[1].x), Random.Range(this.PosArray[0].y, this.PosArray[1].y), 0), Quaternion.identity);
                //obj.transform.localPosition = Vector3.zero;
                obj.transform.SetParent(this.transform);
                obj.SetActive(true);
                this.TrailList.Add(obj);
            }
        }

        private void Update()
        {
            foreach (var item in this.TrailList)
            {
                Vector3 dir = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0).normalized;
                item.transform.Translate(dir * Random.Range(1, 10) * this.Speed * Time.deltaTime);
            }
        }
    }
}