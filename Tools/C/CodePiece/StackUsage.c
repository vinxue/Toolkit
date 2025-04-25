/* Fill specific data to stack, RISC-V assembly code */
clear_stack:
    la t0, __stack_start
    la t1, __stack_end
    li t2, 0x5A5A5A5A

clear_loop:
    sw t2, 0(t0)
    addi t0, t0, 4
    bne t0, t1, clear_loop


/* C code to get size of stack usage */
extern unsigned int __stack_start;
extern unsigned int __stack_end;

size_t detect_stack_usage(void)
{
    unsigned int *stack_ptr = (unsigned int *)&__stack_start;
    unsigned int *stack_end = (unsigned int *)&__stack_end;
    size_t used = 0;

    while (stack_ptr < stack_end) {
        if (*stack_ptr != 0x5A5A5A5A) {
            used = (stack_end - stack_ptr) * sizeof(unsigned int);
            break;
        }
        stack_ptr++;
    }

    return used;
}
