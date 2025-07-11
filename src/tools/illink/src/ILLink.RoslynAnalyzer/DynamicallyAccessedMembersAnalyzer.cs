// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using ILLink.RoslynAnalyzer.TrimAnalysis;
using ILLink.Shared;
using ILLink.Shared.TrimAnalysis;
using ILLink.Shared.TypeSystemProxy;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ILLink.RoslynAnalyzer
{
	[DiagnosticAnalyzer (LanguageNames.CSharp)]
	public sealed class DynamicallyAccessedMembersAnalyzer : DiagnosticAnalyzer
	{
		internal const string DynamicallyAccessedMembers = nameof (DynamicallyAccessedMembers);
		internal const string DynamicallyAccessedMembersAttribute = nameof (DynamicallyAccessedMembersAttribute);
		public const string attributeArgument = "attributeArgument";
		public const string FullyQualifiedDynamicallyAccessedMembersAttribute = "System.Diagnostics.CodeAnalysis." + DynamicallyAccessedMembersAttribute;
		public const string FullyQualifiedFeatureGuardAttribute  = "System.Diagnostics.CodeAnalysis.FeatureGuardAttribute";
		public static Lazy<ImmutableArray<RequiresAnalyzerBase>> RequiresAnalyzers { get; } = new Lazy<ImmutableArray<RequiresAnalyzerBase>> (GetRequiresAnalyzers);
		static ImmutableArray<RequiresAnalyzerBase> GetRequiresAnalyzers () =>
			ImmutableArray.Create<RequiresAnalyzerBase> (
				new RequiresAssemblyFilesAnalyzer (),
				new RequiresUnreferencedCodeAnalyzer (),
				new RequiresDynamicCodeAnalyzer ());

		public static ImmutableArray<DiagnosticDescriptor> GetSupportedDiagnostics ()
		{
			var diagDescriptorsArrayBuilder = ImmutableArray.CreateBuilder<DiagnosticDescriptor> (26);
			diagDescriptorsArrayBuilder.Add (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.RequiresUnreferencedCode));
			diagDescriptorsArrayBuilder.Add (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.DynamicallyAccessedMembersIsNotAllowedOnMethods));
			AddRange (DiagnosticId.MethodParameterCannotBeStaticallyDetermined, DiagnosticId.DynamicallyAccessedMembersMismatchTypeArgumentTargetsGenericParameter);
			AddRange (DiagnosticId.DynamicallyAccessedMembersOnFieldCanOnlyApplyToTypesOrStrings, DiagnosticId.DynamicallyAccessedMembersOnPropertyCanOnlyApplyToTypesOrStrings);
			diagDescriptorsArrayBuilder.Add (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.DynamicallyAccessedMembersOnMethodReturnValueCanOnlyApplyToTypesOrStrings));
			diagDescriptorsArrayBuilder.Add (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.DynamicallyAccessedMembersFieldAccessedViaReflection));
			diagDescriptorsArrayBuilder.Add (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.DynamicallyAccessedMembersMethodAccessedViaReflection));
			diagDescriptorsArrayBuilder.Add (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.DynamicallyAccessedMembersOnTypeReferencesMemberWithRequiresUnreferencedCode));
			diagDescriptorsArrayBuilder.Add (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.DynamicallyAccessedMembersOnTypeReferencesMemberOnBaseWithRequiresUnreferencedCode));
			diagDescriptorsArrayBuilder.Add (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.DynamicallyAccessedMembersOnTypeReferencesMemberWithDynamicallyAccessedMembers));
			diagDescriptorsArrayBuilder.Add (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.DynamicallyAccessedMembersOnTypeReferencesMemberOnBaseWithDynamicallyAccessedMembers));
			diagDescriptorsArrayBuilder.Add (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.UnrecognizedTypeInRuntimeHelpersRunClassConstructor));
			diagDescriptorsArrayBuilder.Add (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.DynamicallyAccessedMembersMismatchOnMethodReturnValueBetweenOverrides));
			diagDescriptorsArrayBuilder.Add (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.DynamicallyAccessedMembersMismatchOnMethodParameterBetweenOverrides));
			diagDescriptorsArrayBuilder.Add (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.DynamicallyAccessedMembersMismatchOnGenericParameterBetweenOverrides));
			diagDescriptorsArrayBuilder.Add (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.DynamicallyAccessedMembersMismatchOnImplicitThisBetweenOverrides));
			diagDescriptorsArrayBuilder.Add (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.DynamicallyAccessedMembersConflictsBetweenPropertyAndAccessor));
			diagDescriptorsArrayBuilder.Add (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.PropertyAccessorParameterInLinqExpressionsCannotBeStaticallyDetermined));
			diagDescriptorsArrayBuilder.Add (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.MakeGenericType));
			diagDescriptorsArrayBuilder.Add (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.MakeGenericMethod));
			diagDescriptorsArrayBuilder.Add (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.CaseInsensitiveTypeGetTypeCallIsNotSupported));
			diagDescriptorsArrayBuilder.Add (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.UnrecognizedTypeNameInTypeGetType));
			diagDescriptorsArrayBuilder.Add (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.UnrecognizedParameterInMethodCreateInstance));
			diagDescriptorsArrayBuilder.Add (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.ParametersOfAssemblyCreateInstanceCannotBeAnalyzed));
			diagDescriptorsArrayBuilder.Add (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.ReturnValueDoesNotMatchFeatureGuards));
			diagDescriptorsArrayBuilder.Add (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.InvalidFeatureGuard));
			diagDescriptorsArrayBuilder.Add (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.TypeMapGroupTypeCannotBeStaticallyDetermined));

			foreach (var requiresAnalyzer in RequiresAnalyzers.Value) {
				foreach (var diagnosticDescriptor in requiresAnalyzer.SupportedDiagnostics)
					diagDescriptorsArrayBuilder.Add (diagnosticDescriptor);
			}

			return diagDescriptorsArrayBuilder.ToImmutable ();

			void AddRange (DiagnosticId first, DiagnosticId last)
			{
				Debug.Assert ((int) first < (int) last);

				for (int i = (int) first;
					i <= (int) last; i++) {
					diagDescriptorsArrayBuilder.Add (DiagnosticDescriptors.GetDiagnosticDescriptor ((DiagnosticId) i));
				}
			}
		}

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => GetSupportedDiagnostics ();

		internal static Location GetPrimaryLocation (ImmutableArray<Location>? locations) {
			if (locations is null)
				return Location.None;

			return locations.Value.Length > 0 ? locations.Value[0] : Location.None;
		}

		public override void Initialize (AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis (GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

			if (!Debugger.IsAttached)
				context.EnableConcurrentExecution ();

			context.RegisterCompilationStartAction (context => {
				var dataFlowAnalyzerContext = DataFlowAnalyzerContext.Create (context.Options, context.Compilation, RequiresAnalyzers.Value);
				if (!dataFlowAnalyzerContext.AnyAnalyzersEnabled)
					return;

				context.RegisterOperationBlockAction (context => {
					foreach (var operationBlock in context.OperationBlocks) {
						TrimDataFlowAnalysis trimDataFlowAnalysis = new (context, dataFlowAnalyzerContext, operationBlock);
						trimDataFlowAnalysis.InterproceduralAnalyze ();
						trimDataFlowAnalysis.ReportDiagnostics (context.ReportDiagnostic);
					}
				});

				// Remaining actions are only for DynamicallyAccessedMembers analysis.
				if (!dataFlowAnalyzerContext.EnableTrimAnalyzer)
					return;

				// Examine generic instantiations in base types and interface list
				context.RegisterSymbolAction (context => {
					var type = (INamedTypeSymbol) context.Symbol;
					// RUC on type doesn't silence DAM warnings about generic base/interface types.
					// This knowledge lives in IsInRequiresUnreferencedCodeAttributeScope,
					// which we still call for consistency here, but it is expected to return false.
					if (type.IsInRequiresUnreferencedCodeAttributeScope (out _))
						return;

					var location = GetPrimaryLocation (type.Locations);

					if (type.BaseType is INamedTypeSymbol baseType)
						GenericArgumentDataFlow.ProcessGenericArgumentDataFlow (location, baseType, context.ReportDiagnostic);

					foreach (var interfaceType in type.Interfaces)
						GenericArgumentDataFlow.ProcessGenericArgumentDataFlow (location, interfaceType, context.ReportDiagnostic);

					DynamicallyAccessedMembersTypeHierarchy.ApplyDynamicallyAccessedMembersToTypeHierarchy (location, type, context.ReportDiagnostic);
				}, SymbolKind.NamedType);
				context.RegisterSymbolAction (context => {
					VerifyMemberOnlyApplyToTypesOrStrings (context, context.Symbol);
					VerifyDamOnPropertyAndAccessorMatch (context, (IMethodSymbol) context.Symbol);
					VerifyDamOnDerivedAndBaseMethodsMatch (context, (IMethodSymbol) context.Symbol);
				}, SymbolKind.Method);
				context.RegisterSymbolAction (context => {
					VerifyDamOnInterfaceAndImplementationMethodsMatch (context, (INamedTypeSymbol) context.Symbol);
				}, SymbolKind.NamedType);
				context.RegisterSymbolAction (context => {
					VerifyMemberOnlyApplyToTypesOrStrings (context, context.Symbol);
				}, SymbolKind.Property);
				context.RegisterSymbolAction (context => {
					VerifyMemberOnlyApplyToTypesOrStrings (context, context.Symbol);
				}, SymbolKind.Field);
			});
		}

		static void VerifyMemberOnlyApplyToTypesOrStrings (SymbolAnalysisContext context, ISymbol member)
		{
			var location = GetPrimaryLocation (member.Locations);
			if (member is IFieldSymbol field && field.GetDynamicallyAccessedMemberTypes () != DynamicallyAccessedMemberTypes.None && !field.Type.IsTypeInterestingForDataflow (isByRef: field.RefKind is not RefKind.None))
				context.ReportDiagnostic (Diagnostic.Create (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.DynamicallyAccessedMembersOnFieldCanOnlyApplyToTypesOrStrings), location, member.GetDisplayName ()));
			else if (member is IMethodSymbol method) {
				if (method.GetDynamicallyAccessedMemberTypesOnReturnType () != DynamicallyAccessedMemberTypes.None && !method.ReturnType.IsTypeInterestingForDataflow (isByRef: method.ReturnsByRef))
					context.ReportDiagnostic (Diagnostic.Create (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.DynamicallyAccessedMembersOnMethodReturnValueCanOnlyApplyToTypesOrStrings), location, member.GetDisplayName ()));
				if (method.GetDynamicallyAccessedMemberTypes () != DynamicallyAccessedMemberTypes.None && !method.ContainingType.IsTypeInterestingForDataflow (isByRef: false))
					context.ReportDiagnostic (Diagnostic.Create (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.DynamicallyAccessedMembersIsNotAllowedOnMethods), location));
				foreach (var parameter in method.Parameters) {
					if (parameter.GetDynamicallyAccessedMemberTypes () != DynamicallyAccessedMemberTypes.None && !parameter.Type.IsTypeInterestingForDataflow (isByRef: parameter.RefKind is not RefKind.None))
						context.ReportDiagnostic (Diagnostic.Create (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.DynamicallyAccessedMembersOnMethodParameterCanOnlyApplyToTypesOrStrings), location, parameter.GetDisplayName (), member.GetDisplayName ()));
				}
			} else if (member is IPropertySymbol property && property.GetDynamicallyAccessedMemberTypes () != DynamicallyAccessedMemberTypes.None && !property.Type.IsTypeInterestingForDataflow (isByRef: property.ReturnsByRef)) {
				context.ReportDiagnostic (Diagnostic.Create (DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.DynamicallyAccessedMembersOnPropertyCanOnlyApplyToTypesOrStrings), location, member.GetDisplayName ()));
			}
		}

		static void VerifyDamOnDerivedAndBaseMethodsMatch (SymbolAnalysisContext context, IMethodSymbol methodSymbol)
		{
			if (methodSymbol.TryGetOverriddenMember (out var overriddenSymbol) && overriddenSymbol is IMethodSymbol overriddenMethod
				&& context.Symbol is IMethodSymbol method) {
				VerifyDamOnMethodsMatch (context, method, overriddenMethod);
			}
		}

		static void VerifyDamOnMethodsMatch (SymbolAnalysisContext context, IMethodSymbol overrideMethod, IMethodSymbol baseMethod, ISymbol? origin = null)
		{
			var overrideMethodReturnAnnotation = FlowAnnotations.GetMethodReturnValueAnnotation (overrideMethod);
			var baseMethodReturnAnnotation = FlowAnnotations.GetMethodReturnValueAnnotation (baseMethod);
			if (overrideMethodReturnAnnotation != baseMethodReturnAnnotation) {

				(IMethodSymbol attributableMethod, DynamicallyAccessedMemberTypes missingAttribute) = GetTargetAndRequirements (overrideMethod,
					baseMethod, overrideMethodReturnAnnotation, baseMethodReturnAnnotation);

				Location attributableSymbolLocation = GetPrimaryLocation (attributableMethod.Locations);

				// code fix does not support merging multiple attributes. If an attribute is present or the method is not in source, do not provide args for code fix.
				(Location[]? sourceLocation, Dictionary<string, string?>? DAMArgs) = (!attributableSymbolLocation.IsInSource
					|| (overrideMethod.TryGetReturnAttribute (DynamicallyAccessedMembersAnalyzer.DynamicallyAccessedMembersAttribute, out var _)
						&& baseMethod.TryGetReturnAttribute (DynamicallyAccessedMembersAnalyzer.DynamicallyAccessedMembersAttribute, out var _))
						) ? (null, null) : CreateArguments (attributableSymbolLocation, missingAttribute);

				var returnOrigin = origin ??= overrideMethod;
				context.ReportDiagnostic (Diagnostic.Create (
					DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.DynamicallyAccessedMembersMismatchOnMethodReturnValueBetweenOverrides),
					GetPrimaryLocation (returnOrigin.Locations), sourceLocation, DAMArgs?.ToImmutableDictionary (), overrideMethod.GetDisplayName (), baseMethod.GetDisplayName ()));
			}

			foreach (var overrideParam in overrideMethod.GetMetadataParameters ()) {
				var baseParam = baseMethod.GetParameter (overrideParam.Index);
				var baseParameterAnnotation = FlowAnnotations.GetMethodParameterAnnotation (baseParam);
				var overrideParameterAnnotation = FlowAnnotations.GetMethodParameterAnnotation (overrideParam);
				if (overrideParameterAnnotation != baseParameterAnnotation) {
					(IMethodSymbol attributableMethod, DynamicallyAccessedMemberTypes missingAttribute) = GetTargetAndRequirements (overrideMethod,
						baseMethod, overrideParameterAnnotation, baseParameterAnnotation);

					Location attributableSymbolLocation = attributableMethod.GetParameter (overrideParam.Index).Location!;

					// code fix does not support merging multiple attributes. If an attribute is present or the method is not in source, do not provide args for code fix.
					(Location[]? sourceLocation, Dictionary<string, string?>? DAMArgs) = (!attributableSymbolLocation.IsInSource
						|| (overrideParam.ParameterSymbol!.TryGetAttribute (DynamicallyAccessedMembersAnalyzer.DynamicallyAccessedMembersAttribute, out var _)
							&& baseParam.ParameterSymbol!.TryGetAttribute (DynamicallyAccessedMembersAnalyzer.DynamicallyAccessedMembersAttribute, out var _))
							) ? (null, null) : CreateArguments (attributableSymbolLocation, missingAttribute);

					var parameterOrigin = origin ?? overrideParam.ParameterSymbol;
					context.ReportDiagnostic (Diagnostic.Create (
						DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.DynamicallyAccessedMembersMismatchOnMethodParameterBetweenOverrides),
						GetPrimaryLocation (parameterOrigin?.Locations), sourceLocation, DAMArgs?.ToImmutableDictionary (),
						overrideParam.GetDisplayName (), overrideMethod.GetDisplayName (), baseParam.GetDisplayName (), baseMethod.GetDisplayName ()));
				}
			}

			for (int i = 0; i < overrideMethod.TypeParameters.Length; i++) {
				var methodTypeParameterAnnotation = overrideMethod.TypeParameters[i].GetDynamicallyAccessedMemberTypes ();
				var overriddenMethodTypeParameterAnnotation = baseMethod.TypeParameters[i].GetDynamicallyAccessedMemberTypes ();
				if (methodTypeParameterAnnotation != overriddenMethodTypeParameterAnnotation) {

					(IMethodSymbol attributableMethod, DynamicallyAccessedMemberTypes missingAttribute) = GetTargetAndRequirements (overrideMethod, baseMethod, methodTypeParameterAnnotation, overriddenMethodTypeParameterAnnotation);

					var attributableSymbol = attributableMethod.TypeParameters[i];
					Location attributableSymbolLocation = GetPrimaryLocation (attributableSymbol.Locations);

					// code fix does not support merging multiple attributes. If an attribute is present or the method is not in source, do not provide args for code fix.
					(Location[]? sourceLocation, Dictionary<string, string?>? DAMArgs) = (!attributableSymbolLocation.IsInSource
						|| (overrideMethod.TypeParameters[i].TryGetAttribute (DynamicallyAccessedMembersAnalyzer.DynamicallyAccessedMembersAttribute, out var _)
							&& baseMethod.TypeParameters[i].TryGetAttribute (DynamicallyAccessedMembersAnalyzer.DynamicallyAccessedMembersAttribute, out var _))
							) ? (null, null) : CreateArguments (attributableSymbolLocation, missingAttribute);

					var typeParameterOrigin = origin ?? overrideMethod.TypeParameters[i];
					context.ReportDiagnostic (Diagnostic.Create (
						DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.DynamicallyAccessedMembersMismatchOnGenericParameterBetweenOverrides),
						GetPrimaryLocation (typeParameterOrigin.Locations), sourceLocation, DAMArgs?.ToImmutableDictionary (),
						overrideMethod.TypeParameters[i].GetDisplayName (), overrideMethod.GetDisplayName (),
						baseMethod.TypeParameters[i].GetDisplayName (), baseMethod.GetDisplayName ()));
				}
			}

			if (!overrideMethod.IsStatic) {
				var overrideMethodThisAnnotation = FlowAnnotations.GetMethodParameterAnnotation (new ParameterProxy (new (overrideMethod), (ParameterIndex) 0));
				var baseMethodThisAnnotation = FlowAnnotations.GetMethodParameterAnnotation (new ParameterProxy (new (baseMethod), (ParameterIndex) 0));
				if (overrideMethodThisAnnotation != baseMethodThisAnnotation) {
					var methodOrigin = origin ?? overrideMethod;
					context.ReportDiagnostic (Diagnostic.Create (
						DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.DynamicallyAccessedMembersMismatchOnImplicitThisBetweenOverrides),
						GetPrimaryLocation (methodOrigin.Locations),
						overrideMethod.GetDisplayName (), baseMethod.GetDisplayName ()));
				}
			}
		}

		static void VerifyDamOnInterfaceAndImplementationMethodsMatch (SymbolAnalysisContext context, INamedTypeSymbol type)
		{
			foreach (var (interfaceMember, implementationMember) in type.GetMemberInterfaceImplementationPairs ()) {
				if (implementationMember is IMethodSymbol implementationMethod && interfaceMember is IMethodSymbol interfaceMethod) {
					ISymbol origin = implementationMethod;
					INamedTypeSymbol implementationType = implementationMethod.ContainingType;

					// If this type implements an interface method through a base class, the origin of the warning is this type,
					// not the member on the base class.
					if (!implementationType.IsInterface () && !SymbolEqualityComparer.Default.Equals (implementationType, type))
						origin = type;

					VerifyDamOnMethodsMatch (context, implementationMethod, interfaceMethod, origin);
				}
			}
		}

		static void VerifyDamOnPropertyAndAccessorMatch (SymbolAnalysisContext context, IMethodSymbol methodSymbol)
		{
			if ((methodSymbol.MethodKind != MethodKind.PropertyGet && methodSymbol.MethodKind != MethodKind.PropertySet)
				|| methodSymbol.AssociatedSymbol is not IPropertySymbol propertySymbol
				|| !propertySymbol.Type.IsTypeInterestingForDataflow (isByRef: propertySymbol.RefKind is not RefKind.None)
				|| propertySymbol.GetDynamicallyAccessedMemberTypes () == DynamicallyAccessedMemberTypes.None)
				return;

			// None on the return type of 'get' matches unannotated
			if (methodSymbol.MethodKind == MethodKind.PropertyGet
				&& methodSymbol.GetDynamicallyAccessedMemberTypesOnReturnType () != DynamicallyAccessedMemberTypes.None
				// None on parameter of 'set' matches unannotated
				|| methodSymbol.MethodKind == MethodKind.PropertySet
				&& methodSymbol.Parameters[methodSymbol.Parameters.Length - 1].GetDynamicallyAccessedMemberTypes () != DynamicallyAccessedMemberTypes.None) {
				context.ReportDiagnostic (Diagnostic.Create (
					DiagnosticDescriptors.GetDiagnosticDescriptor (DiagnosticId.DynamicallyAccessedMembersConflictsBetweenPropertyAndAccessor),
					GetPrimaryLocation (propertySymbol.Locations),
					propertySymbol.GetDisplayName (),
					methodSymbol.GetDisplayName ()
				));
				return;
			}
		}

		private static (IMethodSymbol Method, DynamicallyAccessedMemberTypes Requirements) GetTargetAndRequirements (IMethodSymbol method, IMethodSymbol overriddenMethod, DynamicallyAccessedMemberTypes methodAnnotation, DynamicallyAccessedMemberTypes overriddenMethodAnnotation)
		{
			DynamicallyAccessedMemberTypes mismatchedArgument;
			IMethodSymbol paramNeedsAttributes;
			if (methodAnnotation == DynamicallyAccessedMemberTypes.None) {
				mismatchedArgument = overriddenMethodAnnotation;
				paramNeedsAttributes = method;
			} else {
				mismatchedArgument = methodAnnotation;
				paramNeedsAttributes = overriddenMethod;
			}
			return (paramNeedsAttributes, mismatchedArgument);
		}

		private static (Location[]?, Dictionary<string, string?>?) CreateArguments (Location attributableSymbolLocation, DynamicallyAccessedMemberTypes mismatchedArgument)
		{
			Dictionary<string, string?>? DAMArgument = new ();
			Location[]? sourceLocation = new Location[] { attributableSymbolLocation };
			DAMArgument.Add (DynamicallyAccessedMembersAnalyzer.attributeArgument, mismatchedArgument.ToString ());
			return (sourceLocation, DAMArgument);
		}
	}
}
