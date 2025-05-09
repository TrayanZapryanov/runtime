#!/usr/bin/env python3
#
# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
#
#
# Title: superpmi_benchmarks.py
#
# Notes:
#
# Script to perform the superpmi collection while executing the Microbenchmarks in 
# https://github.com/dotnet/performance/tree/master/src/benchmarks/micro and real-world in
# in https://github.com/dotnet/performance/tree/master/src/benchmarks/real-world

import argparse
import re
import sys
import stat
import os
import time

from shutil import copyfile
from coreclr_arguments import *
from jitutil import run_command, ChangeDir, TempDir, copy_directory

# Start of parser object creation.
is_windows = platform.system() == "Windows"
parser = argparse.ArgumentParser(description="description")

parser.add_argument("-performance_directory", help="Path to performance directory")
parser.add_argument("-superpmi_directory", help="Path to superpmi directory")
parser.add_argument("-core_root", help="Path to Core_Root directory")
parser.add_argument("-output_mch_path", help="Absolute path to the mch file to produce")
parser.add_argument("-log_file", help="Name of the log file")
parser.add_argument("-partition_count", help="Total number of partitions")
parser.add_argument("-partition_index", help="Partition index to do the collection for")
parser.add_argument("-arch", help="Architecture")
parser.add_argument("-benchmark_path", help="Benchmark's csproj path in dotnet/performance repo")
parser.add_argument("-benchmark_binary", help="Benchmark binary to execute")
parser.add_argument("--tiered_compilation", action="store_true", help="Sets DOTNET_TieredCompilation=1 when doing collections.")
parser.add_argument("--tiered_pgo", action="store_true", help="Sets DOTNET_TieredCompilation=1 and DOTNET_TieredPGO=1 when doing collections.")
parser.add_argument("--jitoptrepeat_all", action="store_true", help="Sets DOTNET_JitOptRepeat=*.")

def setup_args(args):
    """ Setup the args for SuperPMI to use.

    Args:
        args (ArgParse): args parsed by arg parser

    Returns:
        args (CoreclrArguments)

    """
    coreclr_args = CoreclrArguments(args, require_built_core_root=False, require_built_product_dir=False,
                                    require_built_test_dir=False, default_build_type="Checked")

    coreclr_args.verify(args,
                        "performance_directory",
                        lambda performance_directory: os.path.isdir(performance_directory),
                        "performance_directory doesn't exist")

    coreclr_args.verify(args,
                        "superpmi_directory",
                        lambda superpmi_directory: os.path.isdir(superpmi_directory),
                        "superpmi_directory doesn't exist")

    coreclr_args.verify(args,
                        "output_mch_path",
                        lambda output_mch_path: not os.path.isfile(output_mch_path),
                        "output_mch_path already exist")

    coreclr_args.verify(args,
                        "log_file",
                        lambda log_file: True,  # not os.path.isfile(log_file),
                        "log_file already exist")

    coreclr_args.verify(args,
                        "core_root",
                        lambda core_root: os.path.isdir(core_root),
                        "core_root doesn't exist")

    coreclr_args.verify(args,
                        "partition_count",
                        lambda partition_count: partition_count.isnumeric(),
                        "Unable to set partition_count")

    coreclr_args.verify(args,
                        "partition_index",
                        lambda partition_index: partition_index.isnumeric(),
                        "Unable to set partition_index")

    coreclr_args.verify(args,
                        "arch",
                        lambda arch: arch.lower() in ["x86", "x64", "arm", "arm64"],
                        "Unable to set arch")

    coreclr_args.verify(args,
                        "benchmark_path",
                        lambda unused: True,
                        "Unable to set benchmark_path")

    coreclr_args.verify(args,
                        "benchmark_binary",
                        lambda unused: True,
                        "Unable to set benchmark_binary")

    coreclr_args.verify(args,
                        "tiered_compilation",
                        lambda unused: True,
                        "Unable to set tiered_compilation")

    coreclr_args.verify(args,
                        "tiered_pgo",
                        lambda unused: True,
                        "Unable to set tiered_pgo")

    coreclr_args.verify(args,
                        "jitoptrepeat_all",
                        lambda unused: True,
                        "Unable to set jitoptrepeat_all")

    return coreclr_args


def make_executable(file_name):
    """Make file executable by changing the permission

    Args:
        file_name (string): file to execute
    """
    if is_windows:
        return

    print("Inside make_executable")
    run_command(["ls", "-l", file_name])
    os.chmod(file_name,
             # read+execute for owner
             (stat.S_IRUSR | stat.S_IXUSR) |
             # read+execute for group
             (stat.S_IRGRP | stat.S_IXGRP) |
             # read+execute for other
             (stat.S_IROTH | stat.S_IXOTH))
    run_command(["ls", "-l", file_name])

def build_and_run(coreclr_args, output_mch_name):
    """Build the microbenchmarks/real-world and run them under "superpmi collect"

    Args:
        coreclr_args (CoreClrArguments): Arguments use to drive
        output_mch_name (string): Name of output mch file name
    """
    arch = coreclr_args.arch
    python_path = sys.executable
    core_root = coreclr_args.core_root
    superpmi_directory = coreclr_args.superpmi_directory
    performance_directory = coreclr_args.performance_directory
    log_file = coreclr_args.log_file
    partition_count = coreclr_args.partition_count
    partition_index = coreclr_args.partition_index
    benchmark_path = coreclr_args.benchmark_path
    benchmark_binary = coreclr_args.benchmark_binary
    dotnet_directory = os.path.join(performance_directory, "tools", "dotnet", arch)
    dotnet_exe = os.path.join(dotnet_directory, "dotnet")

    artifacts_directory = os.path.join(performance_directory, "artifacts")
    artifacts_packages_directory = os.path.join(artifacts_directory, "packages")
    project_file = os.path.join(performance_directory, benchmark_path)
    benchmarks_dll = os.path.join(artifacts_directory, benchmark_binary)

    # Workaround https://github.com/dotnet/sdk/issues/23430
    project_file = os.path.realpath(project_file)

    if is_windows:
        shim_name = "%JitName%"
        corerun_exe = "CoreRun.exe"
        script_name = "run_benchmarks.bat"
    else:
        shim_name = "$JitName"
        corerun_exe = "corerun"
        script_name = "run_benchmarks.sh"

    make_executable(dotnet_exe)

    # Start with a "dotnet --info" to see what we've got.
    run_command([dotnet_exe, "--info"])

    tfm = "net10.0"
    os.environ["PERFLAB_TARGET_FRAMEWORKS"] = tfm

    env_for_restore = os.environ.copy()

    if is_windows:
        # Try to work around problem with random NuGet failures in "dotnet restore":
        #   error NU3037: Package 'System.Runtime 4.1.0' from source 'https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json':
        #     The repository primary signature validity period has expired. [C:\h\w\A3B008C0\w\B581097F\u\performance\src\benchmarks\micro\MicroBenchmarks.csproj]
        # Using environment variable specified in https://github.com/NuGet/NuGet.Client/pull/4259.
        env_for_restore["NUGET_EXPERIMENTAL_CHAIN_BUILD_RETRY_POLICY"] = "9,2000"

    # If `dotnet restore` fails, retry.
    num_tries = 3
    for try_num in range(num_tries):
        # On the last try, exit on fail
        exit_on_fail = try_num + 1 == num_tries
        (_, _, return_code) = run_command(
            [dotnet_exe, "restore", project_file, "--packages", artifacts_packages_directory],
            _exit_on_fail=exit_on_fail, _env=env_for_restore)
        if return_code == 0:
            # It succeeded!
            break
        print("Try {} of {} failed with error code {}: trying again".format(try_num + 1, num_tries, return_code))
        # Sleep 5 seconds before trying again
        time.sleep(5)

    run_command(
        [dotnet_exe, "build", project_file, "--configuration", "Release",
         "--framework", tfm, "--no-restore", "/p:NuGetPackageRoot=" + artifacts_packages_directory,
         "-o", artifacts_directory], _exit_on_fail=True)

    # This is specifically for PowerShell.Benchmarks.
    # Because we are using '--corerun' when running the benchmarks, it is not able to resolve the PowerShell dependent assemblies.
    # To make this work, we create a directory called 'core_root' in the directory where the benchmarks_dll lives.
    # Then we copy the relevant PowerShell dependencies to 'core_root'.
    # Then we copy the contents of the original 'core_root' to the benchmarks' 'core_root', potentially overwriting any older core dlls.
    # Then we just use the 'corerun.exe' from the benchmarks' 'core_root'.
    # We make a copy of 'core_root' as to avoid any potential file-system issues.
    if benchmarks_dll.endswith("PowerShell.Benchmarks.dll"):
        benchmarks_dll_dir = os.path.dirname(benchmarks_dll)

        # Create benchmarks' 'core_root'.
        benchmarks_dll_core_root = os.path.join(benchmarks_dll_dir, "core_root")
        if not os.path.exists(benchmarks_dll_core_root):
            os.makedirs(benchmarks_dll_core_root)

        # Begin copy PowerShell dependencies.
        if is_windows:
            RID = "win-" + arch
        elif platform.system() == "Darwin":
            RID = "osx-" + arch
        else:
            RID = "linux-" + arch

        if is_windows:
            copy_directory(os.path.join(benchmarks_dll_dir, "runtimes/win/lib/net7.0/"), benchmarks_dll_core_root, verbose_copy=True)
        else:
            copy_directory(os.path.join(benchmarks_dll_dir, "runtimes/unix/lib/net7.0/"), benchmarks_dll_core_root, verbose_copy=True)

        if is_windows:
            dir_to_microsoft_management_infrastructure_dll = os.path.join(benchmarks_dll_dir, f"runtimes/{RID}/lib/netstandard1.6/")
            if os.path.exists(dir_to_microsoft_management_infrastructure_dll):
                copy_directory(dir_to_microsoft_management_infrastructure_dll, benchmarks_dll_core_root, verbose_copy=True)
        else:
            copy_directory(os.path.join(benchmarks_dll_dir, "runtimes/unix/lib/netstandard1.6/"), benchmarks_dll_core_root, verbose_copy=True)

        dir_to_native = os.path.join(benchmarks_dll_dir, f"runtimes/{RID}/native/")
        if os.path.exists(dir_to_native):
            copy_directory(dir_to_native, benchmarks_dll_core_root, verbose_copy=True)

        if platform.system() == "Darwin":
            copy_directory(os.path.join(benchmarks_dll_dir, "runtimes/osx/native/"), benchmarks_dll_core_root, verbose_copy=True)
        # End copy PowerShell dependencies.

        # Copy the original 'core_root' to the benchmarks' 'core_root'.
        copy_directory(core_root, benchmarks_dll_core_root, verbose_copy=True)

        # Use benchmarks' 'core_root'.
        print(f"Using new 'core_root': {benchmarks_dll_core_root}")
        core_root = benchmarks_dll_core_root

    # common BDN prefix
    collection_command = f"{dotnet_exe} {benchmarks_dll} --corerun {os.path.join(core_root, corerun_exe)} "

    # test specific filters
    if benchmark_binary.lower().startswith("microbenchmarks"):
        collection_command += f"--filter \"*\" --partition-count {partition_count} --partition-index {partition_index} "
    elif benchmark_binary.lower().startswith("demobenchmarks"):
        collection_command += "-f *CollisionBatcherTaskBenchmarks.* *GroupedCollisionTesterBenchmarks.* *GatherScatterBenchmarks.* " \
                              " *OneBodyConstraintBenchmarks.* *TwoBodyConstraintBenchmarks.* *ThreeBodyConstraintBenchmarks.* *FourBodyConstraintBenchmarks.* " \
                              " *SweepBenchmarks.* *ShapeRayBenchmarks.* *ShapePileBenchmark.* *RagdollTubeBenchmark.* "
    else:
        collection_command += "--filter \"*\" "

    # common BDN arguments
    collection_command += "--iterationCount 1 --warmupCount 0 --invocationCount 1 --unrollFactor 1 --strategy ColdStart --logBuildOutput "

    # common BDN environment var settings
    # Disable ReadyToRun so we always JIT R2R methods and collect them
    collection_command += f"--envVars DOTNET_JitName:{shim_name} DOTNET_ReadyToRun:0 "

    # custom BDN environment var settings
    if coreclr_args.tiered_pgo:
        collection_command += "DOTNET_TieredCompilation:1 DOTNET_TieredPGO:1"
    elif coreclr_args.tiered_compilation:
        collection_command += "DOTNET_TieredCompilation:1 DOTNET_TieredPGO:0"
    else:
        collection_command += "DOTNET_TieredCompilation:0"

    if coreclr_args.jitoptrepeat_all:
        collection_command += " DOTNET_JitOptRepeat:\"*\""

    # Generate the execution script in Temp location
    with TempDir() as temp_location:
        script_name = os.path.join(temp_location, script_name)

        contents = []
        # Unset the JitName so dotnet process will not fail
        # Unset TieredCompilation and TieredPGO so the parent BDN process is just running with defaults
        if is_windows:
            contents.append("set JitName=%DOTNET_JitName%")
            contents.append("set DOTNET_JitName=")
            contents.append("set DOTNET_TieredCompilation=")
            contents.append("set DOTNET_TieredPGO=")
        else:
            contents.append("#!/bin/bash")
            contents.append("export JitName=$DOTNET_JitName")
            contents.append("unset DOTNET_JitName")
            contents.append("unset DOTNET_TieredCompilation")
            contents.append("unset DOTNET_TieredPGO")
        contents.append(f"pushd {performance_directory}")
        contents.append(collection_command)

        with open(script_name, "w") as collection_script:
            collection_script.write(os.linesep.join(contents))

        print()
        print(f"{script_name} contents:")
        print("******************************************")
        print(os.linesep.join(contents))
        print("******************************************")

        make_executable(script_name)

        script_args = [python_path,
                       os.path.join(superpmi_directory, "superpmi.py"),
                       "collect",
                       "--clean",
                       "-core_root", core_root,
                       "-log_file", log_file,
                       "-output_mch_path", output_mch_name,
                       "-log_level", "debug"]

        script_args.append(script_name)

        run_command(script_args, _exit_on_fail=True)


def strip_unrelated_mc(coreclr_args, old_mch_filename, new_mch_filename):
    """Perform the post processing of produced .mch file by stripping the method contexts
    that are specific to BenchmarkDotnet boilerplate code and hard

    Args:
        coreclr_args (CoreclrArguments): Arguments
        old_mch_filename (string): Name of source .mch file
        new_mch_filename (string): Name of new .mch file to produce post-processing.
    """
    performance_directory = coreclr_args.performance_directory
    core_root = coreclr_args.core_root
    methods_to_strip_list = os.path.join(performance_directory, "methods_to_strip.mcl")

    mcs_exe = os.path.join(core_root, "mcs")
    mcs_command = [mcs_exe, "-dumpMap", old_mch_filename]

    # Gather method list to strip
    (mcs_out, _, return_code) = run_command(mcs_command)
    if return_code != 0:
        # If strip command fails, then just copy the old_mch to new_mch
        print(f"-dumpMap failed. Copying {old_mch_filename} to {new_mch_filename}.")
        copyfile(old_mch_filename, new_mch_filename)
        copyfile(old_mch_filename + ".mct", new_mch_filename + ".mct")
        return

    method_context_list = mcs_out.decode("utf-8").split(os.linesep)
    filtered_context_list = []

    match_pattern = re.compile('^(\\d+),(BenchmarkDotNet|Perfolizer)')
    print("Method indices to strip:")
    for mc_entry in method_context_list:
        matched = match_pattern.match(mc_entry)
        if matched:
            print(matched.group(1))
            filtered_context_list.append(matched.group(1))
    print(f"Total {len(filtered_context_list)} methods.")

    with open(methods_to_strip_list, "w") as f:
        f.write('\n'.join(filtered_context_list))

    # Strip and produce new .mcs file
    if run_command([mcs_exe, "-strip", methods_to_strip_list, old_mch_filename, new_mch_filename])[2] != 0:
        # If strip command fails, then just copy the old_mch to new_mch
        print(f"-strip failed. Copying {old_mch_filename} to {new_mch_filename}.")
        copyfile(old_mch_filename, new_mch_filename)
        copyfile(old_mch_filename + ".mct", new_mch_filename + ".mct")
        return

    # Create toc file
    run_command([mcs_exe, "-toc", new_mch_filename])


def main(main_args):
    """ Main entry point

    Args:
        main_args ([type]): Arguments to the script
    """
    coreclr_args = setup_args(main_args)

    if coreclr_args.tiered_compilation and coreclr_args.tiered_pgo:
        raise RuntimeError("Pass only one tiering option.")

    all_output_mch_name = os.path.join(coreclr_args.output_mch_path + "_all.mch")
    build_and_run(coreclr_args, all_output_mch_name)
    if os.path.isfile(all_output_mch_name):
        pass
    else:
        print("No mch file generated.")

    strip_unrelated_mc(coreclr_args, all_output_mch_name, coreclr_args.output_mch_path)


if __name__ == "__main__":
    args = parser.parse_args()
    sys.exit(main(args))
