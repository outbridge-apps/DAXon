////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2013-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Expressions.Operators
{
    public class OperandArray : IEnumerable<Operand>
    {
        private readonly Operand[] operandArray;

        public virtual OperandRole[] Roles
        {
            get
            {
                OperandRole[] or = new OperandRole[operandArray.Length];
                for (int i = 0; i < or.Length; i++)
                {
                    or[i] = operandArray[i].OperandRole;
                }

                return or;
            }
        }

        public virtual int NumberOfOperands => operandArray.Length;
        public OperandArray(Expression parent, Expression[] args)
        {
            this.operandArray = new Operand[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                operandArray[i] = new Operand(parent, args[i], OperandRole.NAVIGATE);
            }
        }

        public OperandArray(Expression parent, Expression[] args, OperandRole[] roles)
        {
            this.operandArray = new Operand[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                operandArray[i] = new Operand(parent, args[i], roles[i]);
            }
        }

        public OperandArray(Expression parent, Expression[] args, OperandRole role)
        {
            this.operandArray = new Operand[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                operandArray[i] = new Operand(parent, args[i], role);
            }
        }

        public virtual IEnumerator<Operand> IIterator()
        {
            return ((IEnumerable<Operand>)operandArray).GetEnumerator();
        }

        public virtual Operand[] Copy()
        {
            return ArrayTools.CopyOf(operandArray, operandArray.Length);
        }

        public virtual Operand GetOperand(int n)
        {
            try
            {
                return operandArray[n];
            }
            catch (IndexOutOfRangeException)
            {
                throw new ArgumentException();
            }
        }

        public virtual Expression GetOperandExpression(int n)
        {
            try
            {
                return operandArray[n].GetChildExpression();
            }
            catch (IndexOutOfRangeException a)
            {
                throw new ArgumentException(a.Message, a);
            }
        }

        public virtual IEnumerable<Operand> Operands()
        {
            return this;
        }

        public virtual IEnumerable<Expression> OperandExpressions()
        {
            IList<Expression> list = new List<Expression>(operandArray.Length);
            foreach (Operand o in this)
            {
                list.Add(o.GetChildExpression());
            }

            return list;
        }

        public virtual void SetOperand(int n, Expression child)
        {
            try
            {
                if (operandArray[n].GetChildExpression() != child)
                {
                    operandArray[n].SetChildExpression(child);
                }
            }
            catch (IndexOutOfRangeException)
            {
                throw new ArgumentException();
            }
        }

        public static bool Every<T>(T[] args, Func<T, bool> condition)
        {
            foreach (T arg in args)
            {
                if (!condition(arg))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool Some<T>(T[] args, Func<T, bool> condition)
        {
            foreach (T arg in args)
            {
                if (condition(arg))
                {
                    return true;
                }
            }

            return false;
        }
        public IEnumerator<Operand> GetEnumerator() => IIterator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => IIterator();
    }
}