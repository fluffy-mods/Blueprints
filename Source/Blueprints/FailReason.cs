// FailReason.cs
// Copyright Karel Kroeze, 2019-2019

using System;

namespace Blueprints
{
    public struct FailReason : IEquatable<FailReason>
    {
        public bool   success;
        public string reason;

        public FailReason( bool success, string reason = null )
        {
            this.reason  = reason;
            this.success = success;
        }

        public FailReason( string reason ) : this( false, reason )
        {
        }

        public static implicit operator FailReason( string reason )
        {
            return new FailReason( reason );
        }

        public static implicit operator FailReason( bool success )
        {
            return new FailReason( success );
        }

        public static implicit operator bool( FailReason failReason )
        {
            return failReason.success;
        }

        public bool Equals( FailReason other )
        {
            return success == other.success && string.Equals( reason, other.reason );
        }

        public override bool Equals( object obj )
        {
            return obj is FailReason other && Equals( other );
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ( success.GetHashCode() * 397 ) ^ ( reason != null ? reason.GetHashCode() : 0 );
            }
        }

        public static bool operator ==( FailReason left, FailReason right )
        {
            return left.Equals( right );
        }

        public static bool operator !=( FailReason left, FailReason right )
        {
            return !left.Equals( right );
        }
    }
}