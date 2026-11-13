using System;

namespace OutSmart.DAXon.Api
{
    // Null where a public method needs a value is the caller's error: ArgumentNullException naming the parameter.
    internal static class ArgumentCheck
    {
        internal static void NotNull(object value, string name)
        {
            if (value == null)
            {
                throw new ArgumentNullException(name);
            }
        }

        // A number cast to an enum that does not define it: taken for the nearest thing, it changed the behaviour later.
        internal static void Defined<T>(T value, string name) where T : struct, Enum
        {
            if (!Enum.IsDefined(typeof(T), value))
            {
                throw new ArgumentOutOfRangeException(name, value, "Not a value of " + typeof(T).Name);
            }
        }

        // For constructor initializers.
        internal static T Checked<T>(T value, string name) where T : class
        {
            NotNull(value, name);
            return value;
        }

        // A value in a parameter map: an XdmValue, as s9api's <T extends XdmValue> demanded (the port's T is open).
        internal static XdmValue ParameterValue<T>(QName key, T value, string name)
        {
            if (value is XdmValue xdm)
            {
                return xdm;
            }

            if (value == null)
            {
                throw new ArgumentNullException(name, "The value of parameter " + key + " is null");
            }

            throw new ArgumentException("The value of parameter " + key + " is a " + value.GetType().FullName + ", not an XdmValue", name);
        }

        internal static void NoNulls<T>(T[] values, string name) where T : class
        {
            NotNull(values, name);
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == null)
                {
                    throw new ArgumentNullException(name, name + "[" + i + "] is null");
                }
            }
        }
    }
}
