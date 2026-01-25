#!/bin/bash
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
TEST_PROJECTS=(*.Tests)

for TEST_PROJECT in "${TEST_PROJECTS[@]}"; do
    echo $TEST_PROJECT
    cd $SCRIPT_DIR/$TEST_PROJECT
    dotnet run
done