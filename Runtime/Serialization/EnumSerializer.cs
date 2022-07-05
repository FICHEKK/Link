using System;
using System.Linq.Expressions;

namespace Link.Serialization
{
    public class EnumSerializer<TEnum, TNumeric> : ISerializer<TEnum> where TEnum : Enum
    {
        private static readonly ISerializer<TNumeric> NumericSerializer = Serializers.Get<TNumeric>();
        
        public void Write(Packet packet, TEnum @enum)
        {
            var numericValue = Cast<TEnum, TNumeric>.Execute(@enum);
            NumericSerializer.Write(packet, numericValue);
        }

        public TEnum Read(ReadOnlyPacket packet)
        {
            var numericValue = NumericSerializer.Read(packet);
            return Cast<TNumeric, TEnum>.Execute(numericValue);
        }
        
        private static class Cast<TSource, TResult>
        {
            /// <summary>
            /// Compiled method that performs casting from <see cref="TSource"/>
            /// value to <see cref="TResult"/> value.
            /// </summary>
            private static readonly Func<TSource, TResult> Caster = Create();

            /// <summary>
            /// Executes the cast, converting <typeparamref name="TSource"/> to
            /// <typeparamref name="TResult"/>.
            /// </summary>
            public static TResult Execute(TSource source) => Caster(source);

            private static Func<TSource, TResult> Create()
            {
                var parameter = Expression.Parameter(typeof(TSource));
                var converter = Expression.Convert(parameter, typeof(TResult));
                return Expression.Lambda<Func<TSource, TResult>>(converter, parameter).Compile();
            }
        }
    }
}
