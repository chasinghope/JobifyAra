using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using UnityEngine.Jobs;
using Unity.Burst;
using Unity.Collections;
using System.Text;

namespace Game
{
    public class TestClass : MonoBehaviour
    {
        public struct TestJob : IJob
        {
            public void Execute()
            {
                //Mesh mesh = new Mesh();

            }
        }

        private void Update()
        {
            TestJob testJob = new TestJob();
            testJob.Schedule().Complete();
        }
    }
}