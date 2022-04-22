using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Common.Models;
using ICP.Common.Candid;
using ICP.Common.Candid.Constants;
using ICP.Common.Encodings;

namespace ICP.Common.Models
{
    public class CompoundTypeTable
    {
        /// <summary>
        /// Bytes of compound types. The types are referenced by index such as in in `EncodedTypes`
        /// </summary>
        private readonly List<byte[]> EncodedCompoundTypes = new List<byte[]>();

        /// <summary>
        /// A mapping of compound type definition to `EncodedCompoundTypes` index to be used as reference
        /// </summary>
        private readonly Dictionary<CompoundCandidTypeDefinition, int> CompoundTypeIndexMap = new Dictionary<CompoundCandidTypeDefinition, int>();

        public int GetOrAdd(CompoundCandidTypeDefinition typeDef)
        {
            if (!this.CompoundTypeIndexMap.TryGetValue(typeDef, out int index))
            {
                byte[] encodedType = this.EncodeFunc(typeDef);
                this.EncodedCompoundTypes.Add(encodedType);
                index = this.EncodedCompoundTypes.Count - 1;
                this.CompoundTypeIndexMap.Add(typeDef, index);
            }
            return index;
        }

        private byte[] EncodeFunc(CompoundCandidTypeDefinition typeDef)
        {
            byte[] encodedInnerValue = typeDef.EncodeInnerType(this);
            return SLEB128.FromInt64((long)typeDef.Type).Raw
                .Concat(encodedInnerValue)
                .ToArray();
        }

        public IEnumerable<byte> Encode()
        {
            byte[] compoundTypesCount = LEB128.FromUInt64((ulong)this.EncodedCompoundTypes.Count).Raw;
            return compoundTypesCount
                .Concat(this.EncodedCompoundTypes.SelectMany(t => t));
        }

        public static CompoundTypeTable FromTypes(List<CandidTypeDefinition> types)
        {
            var table = new CompoundTypeTable();
            foreach (CandidTypeDefinition type in types)
            {
                table.GetOrAdd(type, )
}
        }

        public class IDLBuilder
        {
            /// <summary>
            /// Helper to capture compound types
            /// </summary>
            private readonly CompoundTypeTable compoundTypeTable = new CompoundTypeTable();
            /// <summary>
            /// Ordered list of encoded types (encoded with SLEB128).
            /// If SLEB value is positive, it is an index for `EncodedCompoundTypes` for a compound type
            /// If SLEB value is negative, it is type code for a primitive value
            /// </summary>
            private readonly List<byte[]> EncodedTypes = new List<byte[]>();
            /// <summary>
            /// Ordered list of encoded values
            /// </summary>
            private readonly List<byte[]> EncodedValues = new List<byte[]>();


            public void Add(CandidValue value, CandidTypeDefinition def)
            {
                byte[] encodedType = def.Encode(this.compoundTypeTable);
                this.EncodedTypes.Add(encodedType);
                byte[] encodedValue = value.EncodeValue();
                this.EncodedValues.Add(encodedValue);
            }

            public static IDLBuilder FromArgs(IEnumerable<(CandidValue, CandidTypeDefinition)> values)
            {
                var builder = new IDLBuilder();
                foreach ((CandidValue value, CandidTypeDefinition def) in values)
                {
                    builder.Add(value, def);
                }
                return builder;
            }

            public byte[] Encode()
            {
                byte[] encodedPrefix = Encoding.UTF8.GetBytes("DIDL");

                IEnumerable<byte> encodedTypes = this.GenerateTypeEncoding();
                IEnumerable<byte> encodedValues = this.GenerateValueEncoding();
                return encodedPrefix
                    .Concat(encodedTypes)
                    .Concat(encodedValues)
                    .ToArray();
            }

            private IEnumerable<byte> GenerateTypeEncoding()
            {
                byte[] encodedTypesCount = LEB128.FromUInt64((ulong)this.EncodedTypes.Count).Raw;

                return this.compoundTypeTable.Encode()
                    .Concat(encodedTypesCount)
                    .Concat(this.EncodedTypes.SelectMany(t => t));
            }

            private IEnumerable<byte> GenerateValueEncoding()
            {
                return this.EncodedValues.SelectMany(v => v);
            }
        }
    }