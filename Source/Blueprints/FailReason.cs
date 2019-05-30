// FailReason.cs
// Copyright Karel Kroeze, 2019-2019

namespace Blueprints
{
    public struct FailReason
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
    }
}