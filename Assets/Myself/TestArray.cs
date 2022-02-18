using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using UnityEngine.Jobs;
using Unity.Burst;
using Unity.Collections;
using System.Text;

public class TestArray : MonoBehaviour
{
    [BurstCompile]
    public struct Head
    {
        public int number;
        public int id;
    }

    [BurstCompile]
    public struct Group
    {
        public NativeList<Head> mHeadList;
        public NativeList<int> weightList;
        public int groupId;
    }

    public void Update()
    {
        NativeList<Group> groupList = new NativeList<Group>(Allocator.TempJob);
        for (int i = 0; i < 3; i++)
        {
            Group group = new Group();
            group.mHeadList = new NativeList<Head>(Allocator.TempJob);
            for (int j = 0; j < 3; j++)
            {
                Head head = new Head
                {
                    id = j,
                    number = j * 10
                };
                group.mHeadList.Add(head);
               
            }

            for (int j = 0; j < 5; j++)
            {
                group.weightList.Add(j);
            }
            group.groupId = i;

            groupList.Add(group);
        }

        StringBuilder builder0 = new StringBuilder("group:");
        for (int i = 0; i < groupList.Length; i++)
        {
            Group group = groupList[i];
            builder0.Append("head");
            for (int j = 0; j < group.mHeadList.Length; j++)
            {
                builder0.Append(group.mHeadList[j].id);
                builder0.Append("    ");
                builder0.Append(group.mHeadList[j].number);
            }
            builder0.Append("weightList");
            for (int j = 0; j < group.weightList.Length; j++)
            {
                builder0.Append(group.weightList[j]);
                builder0.Append("    ");
            }
            builder0.Append("groupId");
            builder0.Append(group.groupId);
        }

        GroupWork groupWork = new GroupWork()
        {
            mGroupList = groupList
        };
        groupWork.Schedule(groupList.Length, 1).Complete();

        StringBuilder builder = new StringBuilder("group:");
        for (int i = 0; i < groupList.Length; i++)
        {
            Group group = groupList[i];
            builder.Append("head");
            for (int j = 0; j < group.mHeadList.Length; j++)
            {
                builder.Append(group.mHeadList[j].id);
                builder.Append("    ");
                builder.Append(group.mHeadList[j].number);
            }
            builder.Append("weightList");
            for (int j = 0; j < group.weightList.Length; j++)
            {
                builder.Append(group.weightList[j]);
                builder.Append("    ");
            }
            builder.Append("groupId");
            builder.Append(group.groupId);
        }

        groupList.Dispose();



    }

    [BurstCompile]
    public struct GroupWork : IJobParallelFor
    {
        public NativeList<Group> mGroupList;

        public void Execute(int index)
        {
            Group group = mGroupList[index];

            for (int i = 0; i < group.mHeadList.Length; i++)
            {
                Head head = group.mHeadList[i];
                head.id += 1;
                head.number *= 5;
                group.mHeadList[i] = head;
            }

            for (int i = 0; i < group.weightList.Length; i++)
            {
                int weight = group.weightList[i];
                weight += 20;
                group.weightList[i] = weight;
            }
            group.groupId += 3;

            mGroupList[index] = group;
        }
    }

}