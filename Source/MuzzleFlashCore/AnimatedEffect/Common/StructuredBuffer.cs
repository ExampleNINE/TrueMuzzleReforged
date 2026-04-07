using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MuzzleFlash
{
    public class StructuredBuffer<T> where T : unmanaged
    {
        private readonly T[] _innerArray;
        private readonly int _size;

        public int Length => _size;

        public T this[int index] {
            get
            {
                return _innerArray[index];
            }
            set
            {
                _innerArray[index] = value;
            }
        }

        public T[] Raw => _innerArray;

        public StructuredBuffer(int size)
        {
            if (size <= 0) throw new IndexOutOfRangeException("Size is empty");
            _innerArray = new T[size];
            this._size = size;
        }
    }
}
