using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace AStarPathfinding
{
    public struct BurstNodeData
    {
        public float G;
        public float H;
        public float F;
        public int ParentIndex;
        public int HeapIndex;
        public int SearchId;
        public byte Closed;
    }

    [BurstCompile]
    public struct AStarBurstJob : IJob
    {
        [ReadOnly] public NativeArray<byte> blocked;
        public NativeArray<BurstNodeData> nodes;
        public NativeArray<int> heap;
        public int gridW;
        public int gridH;
        public int searchEpoch;
        public int2 start;
        public int2 goal;
        public NativeArray<Vector2Int> resultPath;
        public NativeArray<int> resultLength;

        public void Execute()
        {
            if (!blocked.IsCreated || !nodes.IsCreated || !heap.IsCreated || !resultPath.IsCreated || !resultLength.IsCreated)
            {
                return;
            }

            if (!IsInside(start) || !IsInside(goal))
            {
                resultLength[0] = -1;
                return;
            }

            heap[0] = 0;
            int startIndex = ToIndex(start.x, start.y);
            EnsureFresh(startIndex);
            SetNode(startIndex, 0f, 0f, -1, 0);
            Enqueue(startIndex, 0f);

            int currentIndex = -1;
            while (heap[0] > 0)
            {
                currentIndex = Dequeue();
                BurstNodeData current = nodes[currentIndex];
                current.Closed = 1;
                nodes[currentIndex] = current;

                int currentX = currentIndex % gridW;
                int currentY = currentIndex / gridW;
                if (currentX == goal.x && currentY == goal.y)
                {
                    break;
                }

                bool cBlock = false;
                float g = current.G + 1f;

                for (int dir = 0; dir < 8; dir++)
                {
                    int offsetX;
                    int offsetY;
                    switch (dir)
                    {
                        case 0: offsetX = -1; offsetY = 0; break;
                        case 1: offsetX = 1; offsetY = 0; break;
                        case 2: offsetX = 0; offsetY = 1; break;
                        case 3: offsetX = 0; offsetY = -1; break;
                        case 4: offsetX = -1; offsetY = -1; break;
                        case 5: offsetX = -1; offsetY = 1; break;
                        case 6: offsetX = 1; offsetY = -1; break;
                        default: offsetX = 1; offsetY = 1; break;
                    }

                    int nextX = currentX + offsetX;
                    int nextY = currentY + offsetY;
                    if ((uint)nextX >= (uint)gridW || (uint)nextY >= (uint)gridH)
                    {
                        continue;
                    }

                    int nextIndex = ToIndex(nextX, nextY);
                    if (blocked[nextIndex] != 0)
                    {
                        if (dir < 4)
                        {
                            cBlock = true;
                        }
                        continue;
                    }

                    if (dir >= 4 && cBlock)
                    {
                        continue;
                    }

                    EnsureFresh(nextIndex);
                    BurstNodeData neighbour = nodes[nextIndex];
                    if (neighbour.Closed != 0)
                    {
                        continue;
                    }

                    if (!Contains(nextIndex))
                    {
                        neighbour.G = g;
                        neighbour.H = Heuristic(nextX, nextY, currentX, currentY);
                        neighbour.ParentIndex = currentIndex;
                        nodes[nextIndex] = neighbour;
                        Enqueue(nextIndex, neighbour.G + neighbour.H);
                    }
                    else if (g + neighbour.H < neighbour.F)
                    {
                        neighbour.G = g;
                        neighbour.ParentIndex = currentIndex;
                        nodes[nextIndex] = neighbour;
                        UpdatePriority(nextIndex, neighbour.G + neighbour.H);
                    }
                }
            }

            if (currentIndex < 0)
            {
                resultLength[0] = -1;
                return;
            }

            int count = 0;
            int walkIndex = currentIndex;
            while (walkIndex >= 0)
            {
                count++;
                walkIndex = nodes[walkIndex].ParentIndex;
            }

            resultLength[0] = count;
            walkIndex = currentIndex;
            for (int i = count - 1; i >= 0; i--)
            {
                int x = walkIndex % gridW;
                int y = walkIndex / gridW;
                resultPath[i] = new Vector2Int(x, y);
                walkIndex = nodes[walkIndex].ParentIndex;
            }
        }

        private bool IsInside(int2 position)
        {
            return (uint)position.x < (uint)gridW && (uint)position.y < (uint)gridH;
        }

        private int ToIndex(int x, int y)
        {
            return y * gridW + x;
        }

        private float Heuristic(int fromX, int fromY, int toX, int toY)
        {
            int dX = math.abs(fromX - toX);
            int dY = math.abs(fromY - toY);
            return (dX + dY) + ((math.sqrt(2f) - 2f) * math.min(dX, dY));
        }

        private void EnsureFresh(int index)
        {
            BurstNodeData node = nodes[index];
            if (node.SearchId != searchEpoch)
            {
                node.G = 0f;
                node.H = 0f;
                node.F = 0f;
                node.ParentIndex = -1;
                node.HeapIndex = 0;
                node.SearchId = searchEpoch;
                node.Closed = 0;
                nodes[index] = node;
            }
        }

        private void SetNode(int index, float g, float h, int parentIndex, byte closed)
        {
            BurstNodeData node = nodes[index];
            node.G = g;
            node.H = h;
            node.F = g + h;
            node.ParentIndex = parentIndex;
            node.Closed = closed;
            nodes[index] = node;
        }

        private bool Contains(int index)
        {
            int heapIndex = nodes[index].HeapIndex;
            return heapIndex > 0 && heapIndex <= heap[0] && heap[heapIndex] == index;
        }

        private void Enqueue(int index, float priority)
        {
            int count = heap[0] + 1;
            heap[0] = count;
            heap[count] = index;

            BurstNodeData node = nodes[index];
            node.F = priority;
            node.HeapIndex = count;
            nodes[index] = node;

            CascadeUp(index);
        }

        private int Dequeue()
        {
            int result = heap[1];
            int count = heap[0];
            if (count == 1)
            {
                heap[1] = 0;
                heap[0] = 0;

                BurstNodeData onlyNode = nodes[result];
                onlyNode.HeapIndex = 0;
                nodes[result] = onlyNode;
                return result;
            }

            int lastIndex = heap[count];
            heap[1] = lastIndex;
            heap[count] = 0;
            heap[0] = count - 1;

            BurstNodeData lastNode = nodes[lastIndex];
            lastNode.HeapIndex = 1;
            nodes[lastIndex] = lastNode;

            BurstNodeData removed = nodes[result];
            removed.HeapIndex = 0;
            nodes[result] = removed;

            CascadeDown(lastIndex);
            return result;
        }

        private void UpdatePriority(int index, float priority)
        {
            BurstNodeData node = nodes[index];
            float oldPriority = node.F;
            node.F = priority;
            nodes[index] = node;

            if (priority < oldPriority)
            {
                CascadeUp(index);
            }
            else
            {
                CascadeDown(index);
            }
        }
        // CascadeUp 和 CascadeDown 维护堆结构，确保每次节点优先级更新后堆仍然有效，保证 A* 算法的正确性和效率。
        // CascadeUp 从更新节点向上调整堆，直到找到正确位置；CascadeDown 从更新节点向下调整堆，直到找到正确位置。
        private void CascadeUp(int index)
        {
            int heapIndex = nodes[index].HeapIndex;
            while (heapIndex > 1)
            {
                int parentHeapIndex = heapIndex >> 1;
                int parentIndex = heap[parentHeapIndex];
                if (nodes[parentIndex].F <= nodes[index].F)
                {
                    break;
                }

                Swap(heapIndex, parentHeapIndex);
                heapIndex = parentHeapIndex;
            }
        }

        private void CascadeDown(int index)
        {
            int heapIndex = nodes[index].HeapIndex;
            int count = heap[0];
            while (true)
            {
                int left = heapIndex << 1;
                if (left > count)
                {
                    break;
                }

                int smallest = left;
                int right = left + 1;
                if (right <= count && nodes[heap[right]].F < nodes[heap[left]].F)
                {
                    smallest = right;
                }

                if (nodes[index].F <= nodes[heap[smallest]].F)
                {
                    break;
                }

                Swap(heapIndex, smallest);
                heapIndex = smallest;
            }
        }

        private void Swap(int aHeapIndex, int bHeapIndex)
        {
            int aIndex = heap[aHeapIndex];
            int bIndex = heap[bHeapIndex];
            heap[aHeapIndex] = bIndex;
            heap[bHeapIndex] = aIndex;

            BurstNodeData aNode = nodes[aIndex];
            aNode.HeapIndex = bHeapIndex;
            nodes[aIndex] = aNode;

            BurstNodeData bNode = nodes[bIndex];
            bNode.HeapIndex = aHeapIndex;
            nodes[bIndex] = bNode;
        }
    }
}
