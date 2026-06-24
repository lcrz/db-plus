using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DbClient.Wpf.Services
{
    /// <summary>
    /// Un contenedor de Inyección de Dependencias básico y personalizado.
    /// Soporta registro de Singletons y Transients, así como resolución automática de constructores por reflexión.
    /// </summary>
    public class SimpleDependencyContainer
    {
        private readonly Dictionary<Type, ServiceDescriptor> _services = new();

        /// <summary>
        /// Registra un servicio como Singleton (se crea una única instancia para toda la vida del programa).
        /// </summary>
        public void RegisterSingleton<TService, TImplementation>() where TImplementation : TService
        {
            _services[typeof(TService)] = new ServiceDescriptor(typeof(TImplementation), ServiceLifetime.Singleton);
        }

        /// <summary>
        /// Registra una instancia concreta ya creada como Singleton.
        /// </summary>
        public void RegisterSingleton<TService>(TService instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            _services[typeof(TService)] = new ServiceDescriptor(instance, ServiceLifetime.Singleton);
        }

        /// <summary>
        /// Registra un servicio como Transient (se crea una nueva instancia en cada resolución).
        /// </summary>
        public void RegisterTransient<TService, TImplementation>() where TImplementation : TService
        {
            _services[typeof(TService)] = new ServiceDescriptor(typeof(TImplementation), ServiceLifetime.Transient);
        }

        /// <summary>
        /// Resuelve y devuelve la instancia del tipo de servicio especificado.
        /// </summary>
        public TService Resolve<TService>()
        {
            return (TService)Resolve(typeof(TService));
        }

        /// <summary>
        /// Resuelve la instancia para el tipo de objeto solicitado.
        /// </summary>
        public object Resolve(Type serviceType)
        {
            if (!_services.TryGetValue(serviceType, out var descriptor))
            {
                // Si es un tipo concreto no abstracto ni interfaz, intentamos crearlo directamente aunque no esté registrado explícitamente
                if (!serviceType.IsAbstract && !serviceType.IsInterface)
                {
                    return CreateInstance(serviceType);
                }
                throw new InvalidOperationException($"El servicio del tipo '{serviceType.FullName}' no está registrado en el contenedor DI.");
            }

            if (descriptor.Lifetime == ServiceLifetime.Singleton && descriptor.ImplementationInstance != null)
            {
                return descriptor.ImplementationInstance;
            }

            object instance = CreateInstance(descriptor.ImplementationType);

            if (descriptor.Lifetime == ServiceLifetime.Singleton)
            {
                descriptor.ImplementationInstance = instance;
            }

            return instance;
        }

        /// <summary>
        /// Crea una instancia del tipo indicado resolviendo recursivamente sus parámetros del constructor.
        /// </summary>
        private object CreateInstance(Type implementationType)
        {
            ConstructorInfo[] constructors = implementationType.GetConstructors();
            if (constructors.Length == 0)
            {
                return Activator.CreateInstance(implementationType);
            }

            // Seleccionamos el constructor que tenga mayor número de parámetros
            ConstructorInfo constructor = constructors
                .OrderByDescending(c => c.GetParameters().Length)
                .First();

            ParameterInfo[] parameters = constructor.GetParameters();
            object[] constructorArgs = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                constructorArgs[i] = Resolve(parameters[i].ParameterType);
            }

            return constructor.Invoke(constructorArgs);
        }

        private enum ServiceLifetime
        {
            Singleton,
            Transient
        }

        private class ServiceDescriptor
        {
            public Type ImplementationType { get; }
            public ServiceLifetime Lifetime { get; }
            public object ImplementationInstance { get; set; }

            public ServiceDescriptor(Type implementationType, ServiceLifetime lifetime)
            {
                ImplementationType = implementationType;
                Lifetime = lifetime;
            }

            public ServiceDescriptor(object implementationInstance, ServiceLifetime lifetime)
            {
                ImplementationInstance = implementationInstance;
                ImplementationType = implementationInstance.GetType();
                Lifetime = lifetime;
            }
        }
    }
}
