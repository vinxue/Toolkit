#!/usr/bin/env python

import sys
import os

# Remove duplicate lines from a file and save to a new file with '_clean' suffix
def remove_duplicate_lines(input_file):
    base_name, ext = os.path.splitext(input_file)
    output_file = f"{base_name}_clean{ext}"
    
    seen = set()
    with open(input_file, 'r', encoding='utf-8') as infile, \
         open(output_file, 'w', encoding='utf-8') as outfile:
        
        for line in infile:
            if line not in seen:
                seen.add(line)
                outfile.write(line)

if __name__ == "__main__":
    input_file = sys.argv[1]
    remove_duplicate_lines(input_file)
