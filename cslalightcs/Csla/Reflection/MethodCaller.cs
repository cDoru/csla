using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Csla.Properties;
using System.Diagnostics;

namespace Csla.Reflection
{
  internal static class MethodCaller
  {
    const BindingFlags allLevelFlags =
      BindingFlags.FlattenHierarchy |
      BindingFlags.Instance |
      BindingFlags.Public;

    const BindingFlags oneLevelFlags =
      BindingFlags.DeclaredOnly |
      BindingFlags.Instance |
      BindingFlags.Public;

    private const BindingFlags ctorFlags = 
      BindingFlags.Instance | 
      BindingFlags.Public;

    #region Dynamic Constructor Cache

    // TODO: Make dynamic when time permits.
    private static Dictionary<Type, ConstructorInfo> _ctorCache = new Dictionary<Type, ConstructorInfo>();

    private static ConstructorInfo GetCachedConstructor(Type objectType)
    {
      ConstructorInfo result = null;
      if (!_ctorCache.TryGetValue(objectType, out result))
      {
        lock (_ctorCache)
        {
          if (!_ctorCache.TryGetValue(objectType, out result))
          {
            ConstructorInfo info =
              objectType.GetConstructor(ctorFlags, null, Type.EmptyTypes, null);
            result = info; // DynamicMethodHandlerFactory.CreateConstructor(info);
            _ctorCache.Add(objectType, result);
          }
        }
      }
      return result;
    }

    #endregion

    #region Create Instance

    /// <summary>
    /// Uses reflection to create an object using its 
    /// default constructor.
    /// </summary>
    /// <param name="objectType">Type of object to create.</param>
    public static object CreateInstance(Type objectType)
    {
      var ctor = GetCachedConstructor(objectType);
      if (ctor == null)
        throw new NotImplementedException("Default constructor " + Resources.MethodNotImplemented);
      return ctor.Invoke(null);
    }

    #endregion

    /// <summary>
    /// Uses reflection to dynamically invoke a method
    /// if that method is implemented on the target object.
    /// </summary>
    public static object CallMethodIfImplemented(
      object obj, string method, params object[] parameters)
    {
      MethodInfo info = GetMethod(obj.GetType(), method, parameters);
      if (info != null)
        return CallMethod(obj, info, parameters);
      else
        return null;
    }

    /// <summary>
    /// Uses reflection to dynamically invoke a method,
    /// throwing an exception if it is not
    /// implemented on the target object.
    /// </summary>
#if TESTING
    [DebuggerNonUserCode]
#endif
    public static object CallMethod(
      object obj, string method, params object[] parameters)
    {
      MethodInfo info = GetMethod(obj.GetType(), method, parameters);
      if (info == null)
        throw new NotImplementedException(
          method + " " + Resources.MethodNotImplemented);
      return CallMethod(obj, info, parameters);
    }

    /// <summary>
    /// Uses reflection to dynamically invoke a method,
    /// throwing an exception if it is not
    /// implemented on the target object.
    /// </summary>
    public static object CallMethod(
      object obj, MethodInfo info, params object[] parameters)
    {
      // call a private method on the object
      object result;
      try
      {
        result = info.Invoke(obj, parameters);
      }
      catch (Exception e)
      {
        throw new Csla.Reflection.CallMethodException(
          info.Name + " " + Resources.MethodCallFailed, e.InnerException);
      }
      return result;
    }

    /// <summary>
    /// Uses reflection to locate a matching method
    /// on the target object.
    /// </summary>
    public static MethodInfo GetMethod(Type objectType, string method, params object[] parameters)
    {
      MethodInfo result = null;

      // try to find a strongly typed match

      // first see if there's a matching method
      // where all params match types
      result = FindMethod(objectType, method, GetParameterTypes(parameters));

      if (result == null)
      {
        // no match found - so look for any method
        // with the right number of parameters
        result = FindMethod(objectType, method, parameters.Length);
      }

      // no strongly typed match found, get default
      if (result == null)
      {
        try
        {
          result = objectType.GetMethod(method, allLevelFlags);
        }
        catch (AmbiguousMatchException)
        {
          MethodInfo[] methods = objectType.GetMethods();
          foreach (MethodInfo m in methods)
            if (m.Name == method && m.GetParameters().Length == parameters.Length)
            {
              result = m;
              break;
            }
          if (result == null)
            throw;
        }
      }
      return result;
    }

    internal static Type[] GetParameterTypes(object[] parameters)
    {
      List<Type> result = new List<Type>();
      foreach (object item in parameters)
        if (item == null)
          result.Add(typeof(object));
        else
          result.Add(item.GetType());
      return result.ToArray();
    }

    /// <summary>
    /// Returns a business object type based on
    /// the supplied criteria object.
    /// </summary>
    public static Type GetObjectType(object criteria)
    {
      if (criteria.GetType().IsSubclassOf(typeof(CriteriaBase)))
      {
        // get the type of the actual business object
        // from ICriteria
        return ((CriteriaBase)criteria).ObjectType;
      }
      else
      {
        // get the type of the actual business object
        // based on the nested class scheme in the book
        return criteria.GetType().DeclaringType;
      }
    }

    /// <summary>
    /// Returns information about the specified
    /// method, even if the parameter types are
    /// generic and are located in an abstract
    /// generic base class.
    /// </summary>
    public static MethodInfo FindMethod(Type objType, string method, Type[] types)
    {
      MethodInfo info = null;
      do
      {
        //find for a strongly typed match
        info = objType.GetMethod(method, oneLevelFlags, null, types, null);
        if (info != null)
          break;  //match found

        objType = objType.BaseType;
      } while (objType != null);

      return info;
    }

    /// <summary>
    /// Returns information about the specified
    /// method, finding the method based purely
    /// on the method name and number of parameters.
    /// </summary>
    public static MethodInfo FindMethod(Type objType, string method, int parameterCount)
    {
      // walk up the inheritance hierarchy looking
      // for a method with the right number of
      // parameters
      MethodInfo result = null;
      Type currentType = objType;
      do
      {
        MethodInfo info = currentType.GetMethod(method, oneLevelFlags);
        if (info != null)
        {
          if (info.GetParameters().Length == parameterCount)
          {
            // got a match so use it
            result = info;
            break;
          }
        }
        currentType = currentType.BaseType;
      } while (currentType != null);
      return result;
    }
  }
}