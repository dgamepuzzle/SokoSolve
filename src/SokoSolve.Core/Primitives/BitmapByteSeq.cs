﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using VectorInt;

namespace SokoSolve.Core.Primitives
{
    public class BitmapByteSeq : IBitmap
    {
        public readonly static IHashArrayByte HashArrayByte = new HashArrayByte();
        private byte[] memory;
        public int baseIndex;

        public BitmapByteSeq(VectorInt2 size)
        {
            Size           = size;
            this.memory    = new byte[SizeInBytes()];
            this.baseIndex = 0;
        }

        public BitmapByteSeq(IBitmap copy) : this(copy.Size)
        {
            foreach (var p in copy.TruePositions())
            {
                this[p] = true;
            }
        }

        public BitmapByteSeq(byte[] memory, int baseIndex, VectorInt2 size)
        {
            this.memory = memory;
            this.baseIndex = baseIndex;
            Size = size;
        }

        public static int SizeInBytes(VectorInt2 size) => size.X * size.Y / 8 + 1;

        public int        SizeInBytes() => SizeInBytes(Size);
        public int        Width         => Size.X;
        public int        Height        => Size.Y;
        public VectorInt2 Size          { get; }


        public bool this[int x, int y]
        {
            get
            {
                var offset = baseIndex + y * Width + x;
                return BitIndexedByteArray.GetBit(memory, offset);
            }
            set
            {
                var offset = baseIndex + y * Width + x;
                BitIndexedByteArray.SetBit(memory, offset, value);
            }
        }

        public int Count
        {
            get
            {
                var cc = 0;
                for (var yy = 0; yy < Height; yy++)
                for (var xx = 0; xx < Width; xx++)
                    if (this[xx, yy])
                        cc++;
                return cc;
            }
        }

        public bool this[VectorInt2 aPoint]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this[aPoint.X, aPoint.Y];
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => this[aPoint.X, aPoint.Y] = value;
        }

        public IEnumerable<(VectorInt2, bool)> ForEach()
        {
            for (var yy = 0; yy < Height; yy++)
                for (var xx = 0; xx < Width; xx++)
                yield return (new VectorInt2(xx, yy), this[xx, yy]);
        }

        public bool Equals(IBitmap other) => BitmapHelper.Equal(this, other);
        public int CompareTo(IBitmap other) => BitmapHelper.Compare(this, other);
        public override int GetHashCode() => HashArrayByte.GetHashCode(memory);
        public IEnumerable<bool> ForEachValue() => throw new NotImplementedException();

        public override string ToString()
        {
            var rep = new StringBuilder();
            for (var ccy = 0; ccy < Size.Y; ccy++)
            {
                for (var ccx = 0; ccx < Size.X; ccx++)
                    if (this[ccx, ccy]) rep.Append('X');
                    else rep.Append('.');
                rep.Append(Environment.NewLine);
            }

            return rep.ToString();
        }

    }
}