using System;
using Newtonsoft.Json.Linq;

namespace HeraAgent
{
    /// <summary>
    /// Base interface for class-based CLI tools that maintain state
    /// </summary>
    public interface IHeraTool
    {
        /// <summary>
        /// Execute the tool command with parameters
        /// </summary>
        object HandleCommand(JObject parameters);
    }

    /// <summary>
    /// Generic interface for class-based CLI tools that can work with specific parameter types
    /// </summary>
    public interface IHeraTool<T> where T : class, new()
    {
        /// <summary>
        /// Execute the tool command with typed parameters
        /// </summary>
        object HandleCommand(T parameters);
        
        /// <summary>
        /// Optional: Validate parameters before execution
        /// </summary>
        bool Validate(T parameters);
        
        /// <summary>
        /// Optional: Pre-process parameters
        /// </summary>
        T PreProcess(JObject rawParameters);
        
        /// <summary>
        /// Optional: Post-process results
        /// </summary>
        JObject PostProcess(object result);
    }

    /// <summary>
    /// Base abstract class for class-based CLI tools
    /// </summary>
    public abstract class BaseHeraTool : IHeraTool
    {
        public virtual object HandleCommand(JObject parameters)
        {
            // Default implementation that can be overridden
            return HandleCommandGeneric(parameters);
        }

        protected virtual object HandleCommandGeneric(JObject parameters)
        {
            // Parse parameters and execute
            throw new NotImplementedException("Subclasses must implement HandleCommand or override HandleCommandGeneric");
        }
    }

    /// <summary>
    /// Base abstract class for typed CLI tools
    /// </summary>
    public abstract class BaseHeraTool<T> : BaseHeraTool, IHeraTool<T> where T : class, new()
    {
        public virtual bool Validate(T parameters)
        {
            // Default validation - can be overridden
            return true;
        }

        public virtual T PreProcess(JObject rawParameters)
        {
            // Default conversion from JObject to T
            return rawParameters.ToObject<T>();
        }

        public virtual JObject PostProcess(object result)
        {
            // Default conversion from result to JObject
            return JObject.FromObject(result);
        }

        public object HandleCommand(JObject parameters)
        {
            var typedParams = PreProcess(parameters);
            if (!Validate(typedParams))
                return new ErrorResponse("Parameter validation failed");
            var result = HandleCommandInternal(typedParams);
            return PostProcess(result);
        }

        public object HandleCommand(T parameters)
        {
            if (!Validate(parameters))
                return new ErrorResponse("Parameter validation failed");
            var result = HandleCommandInternal(parameters);
            return PostProcess(result);
        }

        protected virtual object HandleCommandInternal(T parameters)
        {
            throw new NotImplementedException("Subclasses must implement HandleCommandInternal");
        }
    }
}