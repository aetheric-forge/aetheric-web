#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "$script_dir/.." && pwd)"

web_solution="$repo_root/AethericForge.Web.slnx"
runtime_root="$repo_root/runtime"
runtime_solution="$runtime_root/src/AethericForge.Runtime.sln"
runtime_solution_dir="$(dirname -- "$runtime_solution")"
solution_folder="Runtime"

command -v git >/dev/null 2>&1 || {
    echo "error: git is required" >&2
    exit 1
}

command -v dotnet >/dev/null 2>&1 || {
    echo "error: dotnet is required" >&2
    exit 1
}

[[ -f "$web_solution" ]] || {
    echo "error: Web solution not found: $web_solution" >&2
    exit 1
}

git -C "$repo_root" submodule update --init --recursive runtime

[[ -f "$runtime_solution" ]] || {
    echo "error: Runtime solution not found: $runtime_solution" >&2
    exit 1
}

runtime_projects=()

while IFS= read -r relative_project; do
    relative_project="${relative_project//\\//}"
    project="$runtime_solution_dir/$relative_project"

    [[ -f "$project" ]] || {
        echo "error: Runtime project listed by the solution was not found: $project" >&2
        exit 1
    }

    runtime_projects+=("$project")
done < <(
    awk -F'"' '
        /^Project\(/ && $6 ~ /\.csproj$/ {
            print $6
        }
    ' "$runtime_solution"
)

(( ${#runtime_projects[@]} > 0 )) || {
    echo "error: No Runtime projects found in $runtime_solution" >&2
    exit 1
}

dotnet sln "$web_solution" add \
    --solution-folder "$solution_folder" \
    "${runtime_projects[@]}"

echo "Added ${#runtime_projects[@]} Runtime projects to $web_solution under /$solution_folder/."
